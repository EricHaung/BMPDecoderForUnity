using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

public class BMPDecoder
{

    tagBITMAPFILEHEADER tag;
    tagBMP_INFOHEADER info;
    List<tagRGBQUAD> colorList;

    public Texture2D Decode(string path)
    {
        FileStream bmpFile = File.Open(path, FileMode.Open);
        Texture2D texture = null;
        tag = new tagBITMAPFILEHEADER();
        info = new tagBMP_INFOHEADER();

        BinaryReader fileReader = new BinaryReader(bmpFile);
        DecodeHeader(fileReader);
        DecodeInfo(fileReader);

        switch (info.biBitCount)
        {
            case 1:
                colorList = new List<tagRGBQUAD>();
                DecodeRGBQUAD(fileReader);
                texture = Decode1BitImage(fileReader);
                break;

            case 4:
                colorList = new List<tagRGBQUAD>();
                DecodeRGBQUAD(fileReader);
                texture = Decode4BitImage(fileReader);
                break;

            case 8:
                colorList = new List<tagRGBQUAD>();
                DecodeRGBQUAD(fileReader);
                texture = Decode8BitImage(fileReader);
                break;

            case 16:
                texture = Decode16BitImage(fileReader);
                break;

            case 24:
                texture = Decode24BitImage(fileReader);
                break;

            case 32:
                texture = Decode32BitImage(fileReader);
                break;
        }

        fileReader.Close();
        bmpFile.Close();


        return texture;
    }

    public Texture2D Decode(byte[] bytes)
    {
        Stream stream = new MemoryStream(bytes);
        Texture2D texture = null;
        tag = new tagBITMAPFILEHEADER();
        info = new tagBMP_INFOHEADER();

        BinaryReader fileReader = new BinaryReader(stream);
        DecodeHeader(fileReader);
        DecodeInfo(fileReader);

        switch (info.biBitCount)
        {
            case 1:
                colorList = new List<tagRGBQUAD>();
                DecodeRGBQUAD(fileReader);
                texture = Decode1BitImage(fileReader);
                break;

            case 4:
                colorList = new List<tagRGBQUAD>();
                DecodeRGBQUAD(fileReader);
                texture = Decode4BitImage(fileReader);
                break;

            case 8:
                colorList = new List<tagRGBQUAD>();
                DecodeRGBQUAD(fileReader);
                texture = Decode8BitImage(fileReader);
                break;

            case 16:
                texture = Decode16BitImage(fileReader);
                break;

            case 24:
                texture = Decode24BitImage(fileReader);
                break;

            case 32:
                texture = Decode32BitImage(fileReader);
                break;
        }

        fileReader.Close();


        return texture;
    }

    private void DecodeHeader(BinaryReader fileReader)
    {
        byte[] bfTypeArray = new byte[2];
        byte[] bfSizeArray = new byte[4];
        byte[] bfReserved1Array = new byte[2];
        byte[] bfReserved2Array = new byte[2];
        byte[] bfOffBitsArray = new byte[4];


        fileReader.Read(bfTypeArray, 0, 2);
        fileReader.Read(bfSizeArray, 0, 4);
        fileReader.Read(bfReserved1Array, 0, 2);
        fileReader.Read(bfReserved2Array, 0, 2);
        fileReader.Read(bfOffBitsArray, 0, 4);

        tag.bfType = System.Text.Encoding.UTF8.GetString(bfTypeArray);
        tag.bfSize = BitConverter.ToInt32(bfSizeArray, 0);
        tag.bfReserved1 = BitConverter.ToInt16(bfReserved1Array, 0);
        tag.bfReserved2 = BitConverter.ToInt16(bfReserved2Array, 0);
        tag.bfOffBits = BitConverter.ToInt32(bfOffBitsArray, 0);
    }

    private void DecodeInfo(BinaryReader fileReader)
    {
        byte[] biSize = new byte[4];
        byte[] biWidth = new byte[4];
        byte[] biHeight = new byte[4];
        byte[] biPlanes = new byte[2];
        byte[] biBitCount = new byte[2];
        byte[] biCompression = new byte[4];
        byte[] biSizeImage = new byte[4];
        byte[] biXPelsPerMeter = new byte[4];
        byte[] biYPelsPerMeter = new byte[4];
        byte[] biClrUsed = new byte[4];
        byte[] biClrImportant = new byte[4];

        fileReader.Read(biSize, 0, 4);
        fileReader.Read(biWidth, 0, 4);
        fileReader.Read(biHeight, 0, 4);
        fileReader.Read(biPlanes, 0, 2);
        fileReader.Read(biBitCount, 0, 2);
        fileReader.Read(biCompression, 0, 4);
        fileReader.Read(biSizeImage, 0, 4);
        fileReader.Read(biXPelsPerMeter, 0, 4);
        fileReader.Read(biYPelsPerMeter, 0, 4);
        fileReader.Read(biClrUsed, 0, 4);
        fileReader.Read(biClrImportant, 0, 4);

        info.biSize = BitConverter.ToInt32(biSize, 0);
        info.biWidth = BitConverter.ToInt32(biWidth, 0);
        info.biHeight = BitConverter.ToInt32(biHeight, 0);
        info.biPlanes = BitConverter.ToInt16(biPlanes, 0);
        info.biBitCount = BitConverter.ToInt16(biBitCount, 0);
        info.biCompression = BitConverter.ToInt32(biCompression, 0);
        info.biSizeImage = BitConverter.ToInt32(biSizeImage, 0);
        info.biXPelsPerMeter = BitConverter.ToInt32(biXPelsPerMeter, 0);
        info.biYPelsPerMeter = BitConverter.ToInt32(biYPelsPerMeter, 0);
        info.biClrUsed = BitConverter.ToInt32(biClrUsed, 0);
        info.biClrImportant = BitConverter.ToInt32(biClrImportant, 0);
    }

    private void DecodeRGBQUAD(BinaryReader fileReader)
    {
        for (int index = 0; index < (int)Math.Pow(2, info.biBitCount); index++)
        {
            tagRGBQUAD quad = new tagRGBQUAD();
            byte rgbB;
            byte rgbG;
            byte rgbR;
            byte rgbReserved;

            rgbB = fileReader.ReadByte();
            rgbG = fileReader.ReadByte();
            rgbR = fileReader.ReadByte();
            rgbReserved = fileReader.ReadByte();


            quad.rgbB = Convert.ToInt16(0x00 | rgbB);
            quad.rgbG = Convert.ToInt16(0x00 | rgbG);
            quad.rgbR = Convert.ToInt16(0x00 | rgbR);
            quad.rgbReserved = Convert.ToInt32(Convert.ToSByte(rgbReserved));
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

        int i = 0;
        for (int y = 0; y < texture.height; y++)
        {
            int bitCount = 0;
            byte value = 0x00;
            for (int x = 0; x < texture.width; x++)
            {
                int k = tag.bfOffBits + i;
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

                texture.SetPixel(x, y, colorList[index].color);
                i += 3;
            }
            i += skip;

            fileReader.BaseStream.Position += skip;
        }

        texture.Apply();

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

        int i = 0;
        for (int y = 0; y < texture.height; y++)
        {
            bool next = true;
            byte value = 0x00;
            for (int x = 0; x < texture.width; x++)
            {
                int k = tag.bfOffBits + i;
                int index = 0;
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
                texture.SetPixel(x, y, colorList[index].color);
                i += 3;
            }
            i += skip;

            fileReader.BaseStream.Position += skip;
        }

        texture.Apply();

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

        int i = 0;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int k = tag.bfOffBits + i;
                byte value = fileReader.ReadByte();
                int index = Convert.ToInt32(value);
                texture.SetPixel(x, y, colorList[index].color);
                i += 3;
            }
            i += skip;

            fileReader.BaseStream.Position += skip;
        }

        texture.Apply();

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

        int i = 0;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int k = tag.bfOffBits + i;
                byte[] value = new byte[2];
                fileReader.Read(value, 0, 2);
                int rgbR;
                int rgbG;
                int rgbB;
                Color pixelColor = new Color(0, 0, 0);

                if (info.biCompression == 0) //RGB 555 0x 0RRRRRGG GGGBBBBB
                {
                    rgbR = Convert.ToInt16((value[1] >> 2) & 0x1F);
                    rgbG = Convert.ToInt16(((value[1] << 3) & 0x18) | ((value[0] >> 5) & 0x07));
                    rgbB = Convert.ToInt16(value[0] & 0x1F);
                    pixelColor = new Color(rgbR / 32f, rgbG / 32f, rgbB / 32f);
                }
                else if (info.biCompression == 3) //RGB 565 0x RRRRRGGG GGGBBBBB
                {
                    rgbR = Convert.ToInt16((value[1] >> 3) & 0x1F);
                    rgbG = Convert.ToInt16(((value[1] << 3) & 0x38) | ((value[0] >> 5) & 0x07));
                    rgbB = Convert.ToInt16(value[0] & 0x1F);
                    pixelColor = new Color(rgbR / 32f, rgbG / 64f, rgbB / 32f);
                }

                texture.SetPixel(x, y, pixelColor);
                i += 3;
            }
            i += skip;

            fileReader.BaseStream.Position += skip;
        }

        texture.Apply();

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

        int i = 0;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int k = tag.bfOffBits + i;
                byte[] value = new byte[3];
                fileReader.Read(value, 0, 3);

                int rgbB = Convert.ToInt16(value[0]);
                int rgbG = Convert.ToInt16(value[1]);
                int rgbR = Convert.ToInt16(value[2]);

                Color pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f);

                texture.SetPixel(x, y, pixelColor);
                i += 3;
            }
            i += skip;

            fileReader.BaseStream.Position += skip;
        }

        texture.Apply();

        return texture;
    }

    private Texture2D Decode32BitImage(BinaryReader fileReader)
    {
        Texture2D texture = new Texture2D(info.biWidth, info.biHeight);

        int skip = 0;

        //Not need skip

        fileReader.BaseStream.Position = tag.bfOffBits;

        int i = 0;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int k = tag.bfOffBits + i;
                byte[] value = new byte[4];
                fileReader.Read(value, 0, 4);

                int rgbB = Convert.ToInt16(value[0]);
                int rgbG = Convert.ToInt16(value[1]);
                int rgbR = Convert.ToInt16(value[2]);
                int rgbA = Convert.ToInt16(value[3]);

                Color pixelColor = new Color(rgbR / 255f, rgbG / 255f, rgbB / 255f, rgbA / 255f);

                texture.SetPixel(x, y, pixelColor);
                i += 3;
            }
            i += skip;

            fileReader.BaseStream.Position += skip;
        }

        texture.Apply();

        return texture;
    }

}

public struct tagBITMAPFILEHEADER
{
    public string bfType;
    public int bfSize;
    public int bfReserved1;
    public int bfReserved2;
    public int bfOffBits;
}

public struct tagBMP_INFOHEADER
{
    public int biSize;
    public int biWidth;
    public int biHeight;
    public int biPlanes;
    public int biBitCount;
    public int biCompression;
    public int biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public int biClrUsed;
    public int biClrImportant;
}

public struct tagRGBQUAD
{
    public int rgbB;
    public int rgbG;
    public int rgbR;
    public int rgbReserved;

    public Color color;
}
