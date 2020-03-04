using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

public class BMPDecoder
{
    tagBITMAPFILEHEADER tag;
    tagBMP_INFOHEADER info;
    tagBITFIELDS bitFields;
    List<tagRGBQUAD> colorList;

    bool bTopDown;

    public Texture2D Decode(string path)
    {
        using (var stream = File.Open(path, FileMode.Open))
            using (var fileReader = new BinaryReader(stream))
                return Decode(fileReader);
    }

    public Texture2D Decode(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
            using (var fileReader = new BinaryReader(stream))
                return Decode(fileReader);
    }

    public Texture2D Decode(BinaryReader fileReader)
    {
        Texture2D texture = null;
        tag = new tagBITMAPFILEHEADER();
        info = new tagBMP_INFOHEADER();

        DecodeHeader(fileReader);
        DecodeInfo(fileReader);

        if (info.biHeight < 0)
        {
            info.biHeight = -info.biHeight;
            bTopDown = true;
        }

        switch (info.biBitCount)
        {
            case 1:
                DecodeRGBQUAD(fileReader);
                texture = Decode1BitImage(fileReader);
                break;

            case 2:
                DecodeRGBQUAD(fileReader);
                texture = Decode2BitImage(fileReader);
                break;

            case 4:
                DecodeRGBQUAD(fileReader);
                if (info.biCompression == 2)
                    texture = Decode4BitRleImage(fileReader);
                else
                    texture = Decode4BitImage(fileReader);
                break;

            case 8:
                DecodeRGBQUAD(fileReader);
                if (info.biCompression == 1)
                    texture = Decode8BitRleImage(fileReader);
                else
                    texture = Decode8BitImage(fileReader);
                break;

            case 16:
                texture = Decode16BitImage(fileReader);
                break;

            case 24:
                texture = Decode24BitImage(fileReader);
                break;

            case 32:
                if (info.biCompression == 0)
                    texture = Decode32BitImage(fileReader);
                else if (info.biCompression == 3 || info.biCompression == 6)
                    texture = Decode32BitCompressionImage(fileReader);
                break;
        }

        texture.Apply();

        return texture;
    }

    private void DecodeHeader(BinaryReader fileReader)
    {
        tag.bfType = System.Text.Encoding.UTF8.GetString(fileReader.ReadBytes(2));
        tag.bfSize = fileReader.ReadInt32();
        tag.bfReserved1 = fileReader.ReadInt16();
        tag.bfReserved2 = fileReader.ReadInt16();
        tag.bfOffBits = fileReader.ReadInt32();
    }

    private void DecodeInfo(BinaryReader fileReader)
    {
        info.biSize = fileReader.ReadInt32();
        if (info.IsCore)
        {
            info.biWidth = fileReader.ReadInt16();
            info.biHeight = fileReader.ReadInt16();
            info.biPlanes = fileReader.ReadInt16();
            info.biBitCount = fileReader.ReadInt16();
        }
        else
        {
            info.biWidth = fileReader.ReadInt32();
            info.biHeight = fileReader.ReadInt32();
            info.biPlanes = fileReader.ReadInt16();
            info.biBitCount = fileReader.ReadInt16();
            if (info.IsOS2V2Lite)
                return;

            info.biCompression = fileReader.ReadInt32();
            info.biSizeImage = fileReader.ReadInt32();
            info.biXPelsPerMeter = fileReader.ReadInt32();
            info.biYPelsPerMeter = fileReader.ReadInt32();
            info.biClrUsed = fileReader.ReadInt32();
            info.biClrImportant = fileReader.ReadInt32();
            DecodeBitFields(fileReader);

            if (info.IsOS2V2)
                fileReader.BaseStream.Position += 64 - 40; //skip other info
            if (info.IsV4)
                fileReader.BaseStream.Position += 108 - 40 - 16; //skip other info
            if (info.IsV5)
                fileReader.BaseStream.Position += 124 - 40 - 16; //skip other info
        }
    }

    private static byte GetShift(uint mask)
    {
        for (byte i = 0; i < 32; i++)
            if ((mask & 1 << i) != 0)
                return i;

        return 0;
    }

    private static double GetMult(uint mask)
    {
        double result = 256d;
        int bits = 0;
        for (int i = 0; i < 32; i++)
            if ((mask & 1 << i) != 0)
            {
                bits++;
                result /= 4d;
            }

        int maxValue = 1 << bits;
        return result * (maxValue + 1);
    }

    private void DecodeBitFields(BinaryReader fileReader)
    {
        bitFields = new tagBITFIELDS();
        if (info.biCompression == 3 || info.biCompression == 6 || info.IsV4 || info.IsV5)
        {
            bitFields.bRedMask = fileReader.ReadUInt32();
            bitFields.bRedShift = GetShift(bitFields.bRedMask);
            bitFields.bRedMult = GetMult(bitFields.bRedMask);

            bitFields.bGreenMask = fileReader.ReadUInt32();
            bitFields.bGreenShift = GetShift(bitFields.bGreenMask);
            bitFields.bGreenMult = GetMult(bitFields.bGreenMask);

            bitFields.bBlueMask = fileReader.ReadUInt32();
            bitFields.bBlueShift = GetShift(bitFields.bBlueMask);
            bitFields.bBlueMult = GetMult(bitFields.bBlueMask);

            if (info.biCompression == 6 || tag.bfOffBits != 66 || info.IsV4 || info.IsV5)
            {
                bitFields.bAlphaMask = fileReader.ReadUInt32();
                bitFields.bAlphaShift = GetShift(bitFields.bAlphaMask);
                bitFields.bAlphaMult = GetMult(bitFields.bAlphaMask);
            }
        }
    }

    private void DecodeRGBQUAD(BinaryReader fileReader)
    {
        colorList = new List<tagRGBQUAD>();
        var biClrUsed = info.biClrUsed > 0 ? info.biClrUsed : (int)Math.Pow(2, info.biBitCount);
        for (int index = 0; index < biClrUsed; index++)
        {
            tagRGBQUAD quad = new tagRGBQUAD();
            quad.rgbB = fileReader.ReadByte();
            quad.rgbG = fileReader.ReadByte();
            quad.rgbR = fileReader.ReadByte();
            if (!info.IsCore)
                quad.rgbReserved = fileReader.ReadByte();

            quad.color = new Color(quad.rgbR / 255.0f, quad.rgbG / 255.0f, quad.rgbB / 255.0f);
            colorList.Add(quad);
        }
    }

    private Texture2D Decode1BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        int rowByteLenght = (int)Mathf.Ceil(texture.width / 8f);
        if (rowByteLenght % 4 != 0)
        {
            skip = 4 - (rowByteLenght % 4);
        }

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            int bitCount = 0;
            byte value = 0x00;
            for (int x = 0; x < texture.width; x++)
            {
                int index = 0;
                switch (bitCount)
                {
                    case 0:
                        bitCount++;
                        value = fileReader.ReadByte();
                        index = Convert.ToInt32((value >> 7) & 0x01);
                        break;

                    case 1:
                        bitCount++;
                        index = Convert.ToInt32((value >> 6) & 0x01);
                        break;

                    case 2:
                        bitCount++;
                        index = Convert.ToInt32((value >> 5) & 0x01);
                        break;

                    case 3:
                        bitCount++;
                        index = Convert.ToInt32((value >> 4) & 0x01);
                        break;

                    case 4:
                        bitCount++;
                        index = Convert.ToInt32((value >> 3) & 0x01);
                        break;

                    case 5:
                        bitCount++;
                        index = Convert.ToInt32((value >> 2) & 0x01);
                        break;

                    case 6:
                        bitCount++;
                        index = Convert.ToInt32((value >> 1) & 0x01);
                        break;

                    case 7:
                        bitCount = 0;
                        index = Convert.ToInt32(value & 0x01);
                        break;
                }

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode2BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        int rowByteLenght = (int)Mathf.Ceil(texture.width / 4f);
        if (rowByteLenght % 4 != 0)
        {
            skip = 4 - (rowByteLenght % 4);
        }

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            int bitCount = 0;
            byte value = 0x00;
            for (int x = 0; x < texture.width; x++)
            {
                int index = 0;
                switch (bitCount)
                {
                    case 0:
                        bitCount++;
                        value = fileReader.ReadByte();
                        index = Convert.ToInt32((value >> 6) & 0x03);
                        break;

                    case 1:
                        bitCount++;
                        index = Convert.ToInt32((value >> 4) & 0x03);
                        break;

                    case 2:
                        bitCount++;
                        index = Convert.ToInt32((value >> 2) & 0x03);
                        break;

                    case 3:
                        bitCount = 0;
                        index = Convert.ToInt32(value & 0x03);
                        break;
                }

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode4BitRleImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        fileReader.BaseStream.Position = tag.bfOffBits;

        int x = 0;
        int y = 0;
        while (fileReader.BaseStream.Position < fileReader.BaseStream.Length)
        {
            byte b1 = fileReader.ReadByte();
            byte b2 = fileReader.ReadByte();

            if (b1 > 0)
            {
                //Repeat color
                bool next = true;
                byte value = b2;
                for (int j = 0; j < b1; j++)
                {
                    int index;
                    if (next)
                    {
                        next = false;
                        index = (value >> 4) & 0x0F;
                    }
                    else
                    {
                        next = true;
                        index = value & 0x0F;
                    }

                    texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
                    x++;
                }
            }
            else if (b2 == 0)
            {
                //Next row
                x = 0;
                y++;
            }
            else if (b2 == 1)
            {
                //Finish
                break;
            }
            else if (b2 == 2)
            {
                //Move
                x += fileReader.ReadByte();
                y += fileReader.ReadByte();
            }
            else
            {
                //Read bytes and color
                bool next = true;
                byte value = 0x00;
                for (int j = 0; j < b2; j++)
                {
                    int index;
                    if (next)
                    {
                        next = false;
                        value = fileReader.ReadByte();
                        index = (value >> 4) & 0x0F;
                    }
                    else
                    {
                        next = true;
                        index = value & 0x0F;
                    }

                    texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
                    x++;
                }

                //skip
                if ((b2 - 1) % 4 < 2)
                {
                    fileReader.BaseStream.Position++;
                }
            }
        }

        return texture;
    }

    private Texture2D Decode4BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        int rowByteLenght = (int)Mathf.Ceil(texture.width / 2f);
        if (rowByteLenght % 4 != 0)
        {
            skip = 4 - (rowByteLenght % 4);
        }

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            bool next = true;
            byte value = 0x00;
            for (int x = 0; x < texture.width; x++)
            {
                int index;
                if (next)
                {
                    next = false;
                    value = fileReader.ReadByte();
                    index = Convert.ToInt32((value >> 4) & 0x0F);
                }
                else
                {
                    next = true;
                    index = Convert.ToInt32(value & 0x0F);
                }
                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode8BitRleImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        fileReader.BaseStream.Position = tag.bfOffBits;

        int x = 0;
        int y = 0;
        while (fileReader.BaseStream.Position < fileReader.BaseStream.Length)
        {
            byte b1 = fileReader.ReadByte();
            byte b2 = fileReader.ReadByte();

            if (b1 > 0)
            {
                //Repeat color
                for (int j = 0; j < b1; j++)
                {
                    int index = b2;
                    texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
                    x++;
                }
            }
            else if (b2 == 0)
            {
                //Next row
                x = 0;
                y++;
            }
            else if (b2 == 1)
            {
                //Finish
                break;
            }
            else if (b2 == 2)
            {
                //Move
                x += fileReader.ReadByte();
                y += fileReader.ReadByte();
            }
            else
            {
                //Read bytes and color
                for (int j = 0; j < b2; j++)
                {
                    int index = fileReader.ReadByte();

                    texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
                    x++;
                }

                //skip
                if (b2 % 2 == 1)
                {
                    fileReader.BaseStream.Position++;
                }
            }
        }

        return texture;
    }

    private Texture2D Decode8BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        int rowByteLenght = texture.width;
        if (rowByteLenght % 4 != 0)
        {
            skip = 4 - (rowByteLenght % 4);
        }

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                byte value = fileReader.ReadByte();
                int index = Convert.ToInt32(value);
                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode16BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        int rowByteLenght = texture.width * 2;
        if (rowByteLenght % 4 != 0)
        {
            skip = 4 - (rowByteLenght % 4);
        }

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                ushort value = fileReader.ReadUInt16();
                Color pixelColor = new Color(0, 0, 0);

                if (info.biCompression == 0) //RGB 555 0x 0RRRRRGG GGGBBBBB
                {
                    int rgbR = Convert.ToInt16((value >> 10) & 0x1F) * 33 / 4;
                    int rgbG = Convert.ToInt16((value >> 5) & 0x1F) * 33 / 4;
                    int rgbB = Convert.ToInt16(value & 0x1F) * 33 / 4;
                    pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);
                }
                else if (info.biCompression == 3 || info.biCompression == 6) //RGB 565 0x RRRRRGGG GGGBBBBB
                {
                    int rgbR = (int)(((value & bitFields.bRedMask) >> bitFields.bRedShift) * bitFields.bRedMult);
                    int rgbG = (int)(((value & bitFields.bGreenMask) >> bitFields.bGreenShift) * bitFields.bGreenMult);
                    int rgbB = (int)(((value & bitFields.bBlueMask) >> bitFields.bBlueShift) * bitFields.bBlueMult);

                    int rgbA = 255;
                    if (bitFields.bAlphaMask != 0)
                        rgbA = (int)(((value & bitFields.bAlphaMask) >> bitFields.bAlphaShift) * bitFields.bAlphaMult);

                    pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f, rgbA / 255f);
                }

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode24BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        if (texture.width * 3 % 4 != 0)
        {
            skip = 4 - (texture.width * 3 % 4);
        }

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                byte[] value = new byte[3];
                fileReader.Read(value, 0, 3);

                int rgbB = Convert.ToInt16(value[0]);
                int rgbG = Convert.ToInt16(value[1]);
                int rgbR = Convert.ToInt16(value[2]);

                Color pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode32BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        //Not need skip

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                byte[] value = new byte[4];
                fileReader.Read(value, 0, 4);

                int rgbB = Convert.ToInt16(value[0]);
                int rgbG = Convert.ToInt16(value[1]);
                int rgbR = Convert.ToInt16(value[2]);
                int rgbReserved = Convert.ToInt16(value[3]);
                var pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
            }
        }

        return texture;
    }

    private Texture2D Decode32BitCompressionImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        //Not need skip

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                uint value = fileReader.ReadUInt32();

                int rgbR = (int)(((value & bitFields.bRedMask) >> bitFields.bRedShift) * bitFields.bRedMult);
                int rgbG = (int)(((value & bitFields.bGreenMask) >> bitFields.bGreenShift) * bitFields.bGreenMult);
                int rgbB = (int)(((value & bitFields.bBlueMask) >> bitFields.bBlueShift) * bitFields.bBlueMult);

                int rgbA = 255;
                if (bitFields.bAlphaMask != 0)
                    rgbA = (int)(((value & bitFields.bAlphaMask) >> bitFields.bAlphaShift) * bitFields.bAlphaMult);

                var pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f, rgbA / 255f);

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }
}

public struct tagBITMAPFILEHEADER
{
    public string bfType;
    public int bfSize;
    public short bfReserved1;
    public short bfReserved2;
    public int bfOffBits;
}

public struct tagBMP_INFOHEADER
{
    public int biSize;
    public int biWidth;
    public int biHeight;
    public short biPlanes;
    public short biBitCount;
    public int biCompression;
    public int biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public int biClrUsed;
    public int biClrImportant;

    public bool IsCore
    {
        get
        {
            return biSize == 12;
        }
    }

    public bool IsOS2V2Lite
    {
        get
        {
            return biSize == 16;
        }
    }

    public bool IsOS2V2
    {
        get
        {
            return biSize == 64;
        }
    }

    public bool IsV3
    {
        get
        {
            return biSize == 40 || biSize == 52 || biSize == 56;
        }
    }

    public bool IsV4
    {
        get
        {
            return biSize == 108;
        }
    }

    public bool IsV5
    {
        get
        {
            return biSize == 124;
        }
    }
}

public struct tagBITFIELDS
{
    public uint bRedMask;
    public uint bGreenMask;
    public uint bBlueMask;
    public uint bAlphaMask;

    public byte bRedShift;
    public byte bGreenShift;
    public byte bBlueShift;
    public byte bAlphaShift;

    public double bRedMult;
    public double bGreenMult;
    public double bBlueMult;
    public double bAlphaMult;
}

public struct tagRGBQUAD
{
    public byte rgbB;
    public byte rgbG;
    public byte rgbR;
    public byte rgbReserved;

    public Color color;
}
