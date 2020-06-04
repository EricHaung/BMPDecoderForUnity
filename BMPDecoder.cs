using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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

        if (info.biCompression == 4 && (info.IsV4 || info.IsV5))
        {
            // return JPEGDecoder.Decode(fileReader);
            throw new Exception("Jpeg not supported");
        }

        if (info.biCompression == 5 && (info.IsV4 || info.IsV5))
        {
            // return PNGDecoder.Decode(fileReader);
            throw new Exception("Png not supported");
        }

        DecodeRGBQUAD(fileReader);

        switch (info.biBitCount)
        {
            case 1:
                if (info.biCompression == 0)
                    texture = Decode1BitImage(fileReader);
                else if (info.biCompression == 3 && (info.IsOS2V2 || info.IsOS2V2Lite))
                    texture = Decode1BitHuffmanImage(fileReader);
                break;

            case 2:
                texture = Decode2BitImage(fileReader);
                break;

            case 4:
                if (info.biCompression == 0)
                    texture = Decode4BitImage(fileReader);
                else if(info.biCompression == 2)
                    texture = Decode4BitRleImage(fileReader);
                break;

            case 8:
                if (info.biCompression == 0)
                    texture = Decode8BitImage(fileReader);
                else if (info.biCompression == 1)
                    texture = Decode8BitRleImage(fileReader);
                break;

            case 16:
                texture = Decode16BitImage(fileReader);
                break;

            case 24:
                if (info.biCompression == 0)
                    texture = Decode24BitImage(fileReader);
                else if (info.biCompression == 4 && (info.IsOS2V2 || info.IsOS2V2Lite))
                    texture = Decode24BitRleImage(fileReader);
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
        tag.bfSize = fileReader.ReadUInt32();
        tag.bfReserved1 = fileReader.ReadUInt16();
        tag.bfReserved2 = fileReader.ReadUInt16();
        tag.bfOffBits = fileReader.ReadUInt32();

        if (tag.bfType == "BA")
        {
            // multiple images, now supporting only one image
            tag.bfType = System.Text.Encoding.UTF8.GetString(fileReader.ReadBytes(2));
            var Size = fileReader.ReadInt32();
            var OffsetToNext = fileReader.ReadInt32();
            tag.bfOffBits = fileReader.ReadUInt32();
        }
    }

    private void DecodeInfo(BinaryReader fileReader)
    {
        info.biSize = fileReader.ReadUInt32();
        if (info.IsCore)
        {
            info.biWidth = fileReader.ReadUInt16();
            info.biHeight = fileReader.ReadUInt16();
            info.biPlanes = fileReader.ReadUInt16();
            info.biBitCount = fileReader.ReadUInt16();
        }
        else
        {
            info.biWidth = fileReader.ReadInt32();
            info.biHeight = fileReader.ReadInt32();
            info.biPlanes = fileReader.ReadUInt16();
            info.biBitCount = fileReader.ReadUInt16();
            if (info.IsOS2V2Lite)
                return;

            info.biCompression = fileReader.ReadUInt32();
            info.biSizeImage = fileReader.ReadUInt32();
            info.biXPelsPerMeter = fileReader.ReadInt32();
            info.biYPelsPerMeter = fileReader.ReadInt32();
            info.biClrUsed = fileReader.ReadUInt32();
            info.biClrImportant = fileReader.ReadUInt32();
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
        int bits = 0;
        for (int i = 0; i < 32; i++)
            if ((mask & 1 << i) != 0)
                bits++;

        int maxValue = 1 << bits;
        return 255.9d / (maxValue - 1);
    }

    private void DecodeBitFields(BinaryReader fileReader)
    {
        bitFields = new tagBITFIELDS();
        if ((info.IsV3 && (info.biCompression == 3 || info.biCompression == 6)) || info.IsV4 || info.IsV5)
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
        if (info.biBitCount > 8 && info.biClrUsed == 0)
            return;

        colorList = new List<tagRGBQUAD>();
        var biClrUsed = info.biClrUsed > 0 ? info.biClrUsed : (uint)Math.Pow(2, info.biBitCount);
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

        int rowByteLenght = (int)Math.Ceiling(texture.width / 8f);
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
                        index = (value >> 7) & 0x01;
                        break;

                    case 1:
                        bitCount++;
                        index = (value >> 6) & 0x01;
                        break;

                    case 2:
                        bitCount++;
                        index = (value >> 5) & 0x01;
                        break;

                    case 3:
                        bitCount++;
                        index = (value >> 4) & 0x01;
                        break;

                    case 4:
                        bitCount++;
                        index = (value >> 3) & 0x01;
                        break;

                    case 5:
                        bitCount++;
                        index = (value >> 2) & 0x01;
                        break;

                    case 6:
                        bitCount++;
                        index = (value >> 1) & 0x01;
                        break;

                    case 7:
                        bitCount = 0;
                        index = value & 0x01;
                        break;
                }

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    //Terminating White Codes (only first 64, look more https://www.ietf.org/rfc/rfc0804.txt)
    static List<string> white64 = new List<string>
    {
        "00110101", "000111", "0111", "1000", "1011", "1100", "1110", "1111",
        "10011", "10100", "00111", "01000", "001000", "000011", "110100", "110101",
        "101010", "101011", "0100111", "0001100", "0001000", "0010111", "0000011", "0000100",
        "0101000", "0101011", "0010011", "0100100", "0011000", "00000010", "00000011", "00011010",
        "00011011", "00010010", "00010011", "00010100", "00010101", "00010110", "00010111", "00101000",
        "00101001", "00101010", "00101011", "00101100", "00101101", "00000100", "00000101", "00001010",
        "00001011", "01010010", "01010011", "01010100", "01010101", "00100100", "00100101", "01011000",
        "01011001", "01011010", "01011011", "01001010", "01001011", "00110010", "00110011", "00110100",
    };

    //Terminating Black Codes (only first 64)
    static List<string> black64 = new List<string>
    {
        "0000110111", "010", "11", "10", "011", "0011", "0010", "00011",
        "000101", "000100", "0000100", "0000101", "0000111", "00000100", "00000111", "000011000",
        "0000010111", "0000011000", "0000001000", "00001100111", "00001101000", "00001101100", "00000110111", "00000101000",
        "00000010111", "00000011000", "000011001010", "000011001011", "000011001100", "000011001101", "000001101000", "000001101001",
        "000001101010", "000001101011", "000011010010", "000011010011", "000011010100", "000011010101", "000011010110", "000011010111",
        "000001101100", "000001101101", "000011011010", "000011011011", "000001010100", "000001010101", "000001010110", "000001010111",
        "000001100100", "000001100101", "000001010010", "000001010011", "000000100100", "000000110111", "000000111000", "000000100111",
        "000000101000", "000001011000", "000001011001", "000000101011", "000000101100", "000001011010", "000001100110", "000001100111",
    };

    private Texture2D Decode1BitHuffmanImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        fileReader.BaseStream.Position = tag.bfOffBits;

        int bitCount = 0;
        byte value = 0x00;
        for (int y = 0; y < texture.height; y++)
        {
            string index = "";
            bool white = true;
            int x = 0;
            while (x < texture.width)
            {
                switch (bitCount)
                {
                    case 0:
                        bitCount++;
                        value = fileReader.ReadByte();
                        index += (value >> 7) & 0x01;
                        break;

                    case 1:
                        bitCount++;
                        index += (value >> 6) & 0x01;
                        break;

                    case 2:
                        bitCount++;
                        index += (value >> 5) & 0x01;
                        break;

                    case 3:
                        bitCount++;
                        index += (value >> 4) & 0x01;
                        break;

                    case 4:
                        bitCount++;
                        index += (value >> 3) & 0x01;
                        break;

                    case 5:
                        bitCount++;
                        index += (value >> 2) & 0x01;
                        break;

                    case 6:
                        bitCount++;
                        index += (value >> 1) & 0x01;
                        break;

                    case 7:
                        bitCount = 0;
                        index += value & 0x01;
                        break;
                }

                //StartImage or EndLine
                if (index == "000000000001")
                {
                    index = "";
                    continue;
                }

                var count = (white ? white64 : black64).IndexOf(index);
                if (count != -1)
                {
                    Color color = colorList[white ? 0 : 1].color;
                    for (int i = 0; i < count; i++)
                        texture.SetPixel(x++, bTopDown ? info.biHeight - y - 1 : y, color);

                    white = !white;
                    index = "";
                }
            }
        }

        return texture;
    }

    private Texture2D Decode2BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        int rowByteLenght = (int)Math.Ceiling(texture.width / 4f);
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
                        index = (value >> 6) & 0x03;
                        break;

                    case 1:
                        bitCount++;
                        index = (value >> 4) & 0x03;
                        break;

                    case 2:
                        bitCount++;
                        index = (value >> 2) & 0x03;
                        break;

                    case 3:
                        bitCount = 0;
                        index = value & 0x03;
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

        int rowByteLenght = (int)Math.Ceiling(texture.width / 2f);
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
                    index = (value >> 4) & 0x0F;
                }
                else
                {
                    next = true;
                    index = value & 0x0F;
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
                byte index = fileReader.ReadByte();
                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, colorList[index].color);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode16BitImage(BinaryReader fileReader, bool fakeAlpha = false)
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
                    int rgbR = ((value >> 10) & 0x1F) * 33 / 4;
                    int rgbG = ((value >> 5) & 0x1F) * 33 / 4;
                    int rgbB = (value & 0x1F) * 33 / 4;
                    int rgbA = ((value >> 15) & 0x01) * 255;

                    if (!fakeAlpha)
                    {
                        if (rgbA > 0)
                            return Decode16BitImage(fileReader, true);

                        rgbA = 255;
                    }

                    pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f, rgbA / 255f);
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

    // https://www.fileformat.info/format/os2bmp/egff.htm
    private Texture2D Decode24BitRleImage(BinaryReader fileReader)
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
                byte rgbB = b2;
                byte rgbG = fileReader.ReadByte();
                byte rgbR = fileReader.ReadByte();

                Color pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);

                //Repeat color
                for (int j = 0; j < b1; j++)
                {
                    texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
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
                    byte rgbB = fileReader.ReadByte();
                    byte rgbG = fileReader.ReadByte();
                    byte rgbR = fileReader.ReadByte();

                    Color pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);

                    texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
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
                byte rgbB = fileReader.ReadByte();
                byte rgbG = fileReader.ReadByte();
                byte rgbR = fileReader.ReadByte();

                Color pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);

                texture.SetPixel(x, bTopDown ? info.biHeight - y - 1 : y, pixelColor);
            }

            fileReader.BaseStream.Position += skip;
        }

        return texture;
    }

    private Texture2D Decode32BitImage(BinaryReader fileReader, bool fakeAlpha = false)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        //Not need skip

        fileReader.BaseStream.Position = tag.bfOffBits;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int rgbB = fileReader.ReadByte();
                int rgbG = fileReader.ReadByte();
                int rgbR = fileReader.ReadByte();
                int rgbA = fileReader.ReadByte();

                if (!fakeAlpha)
                {
                    if (rgbA > 0)
                        return Decode32BitImage(fileReader, true);

                    rgbA = 255;
                }

                var pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f, rgbA / 255f);

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
    public uint bfSize;
    public ushort bfReserved1;
    public ushort bfReserved2;
    public uint bfOffBits;
}

public struct tagBMP_INFOHEADER
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;

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
