using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LaMulanaScript
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("================================");
                Console.WriteLine("LA-MULANA script Decoder/Encoder");
                Console.WriteLine("================================");
                Console.WriteLine();
                Console.WriteLine("ERROR: No input file specified");
                Console.ReadKey();
            }

            string inputFilePath = Path.GetFullPath(args[0]);
            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"ERROR: {Path.GetFileName(inputFilePath)} doesn't exist.");
                Console.ReadKey();
                return;
            }

            string fontCharsFile = Path.GetDirectoryName(inputFilePath) + "\\fontChars.txt";
            string fontChars = "";
            if (File.Exists(fontCharsFile))
            {
                string[] fileText = File.ReadAllLines(fontCharsFile);
                for (int i = 0; i < fileText.Length; i++)
                {
                    fontChars += fileText[i];
                }
                
                if(!uniqueCharacters(fontChars))
                {
                    Console.WriteLine("ERROR: fontChars.txt contains duplicate characters.");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.WriteLine("ERROR: fontChars.txt not found!");
                Console.ReadKey();
                return;
            }

            if (Path.GetExtension(inputFilePath) == ".dat")
            {
                Console.WriteLine($"Decoding {Path.GetFileName(inputFilePath)}...");
                DecodeFile(inputFilePath, fontChars);
            }

            if (Path.GetExtension(inputFilePath) == ".txt")
            {
                Console.WriteLine($"Encoding {Path.GetFileName(inputFilePath)}...");
                EncodeFile(inputFilePath, fontChars);
            }
        }

        static void DecodeFile(string filePath, string fontChars)
        {
            string decodedText = "";
            using (BinaryReader inFile = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                Console.WriteLine($"fontChars.txt contains {fontChars.Length} characters.");

                // do the decoding
                ushort blockCount = (ushort)IPAddress.NetworkToHostOrder(inFile.ReadInt16()); // byteswap if necessary
                short blockSize;

                // decode block
                for (int i = 0; i < blockCount; i++)
                {
                    blockSize = inFile.ReadInt16();
                    blockSize = IPAddress.NetworkToHostOrder(blockSize);    // byteswap if necessary

                    string blockHeader = $"---------------------------------------- BLOCK {i} ({blockSize / 2}) START\n";
                    byte[] block = inFile.ReadBytes(blockSize);
                    string blockFooter = $"\n---------------------------------------- BLOCK {i} END\n";
                    string decodedBlock = blockHeader + DecodeBlock(block, fontChars) + blockFooter;

                    decodedText += decodedBlock;
                }

            }

            string outputTxtFile = Path.GetDirectoryName(filePath) + "\\" + Path.GetFileNameWithoutExtension(filePath) + "_dec.txt";

            using (StreamWriter outFile = new StreamWriter(outputTxtFile, false, Encoding.UTF8))
            {
                outFile.Write(decodedText);
            }
        }

         static void EncodeFile(string textFilePath, string fontChars)
        {
            string decodedText = File.ReadAllText(textFilePath);
            Regex blockRegex = new Regex(@"(?sn)-{40} BLOCK (\d+) \((\d+)\) START(\r\n|\n|\r)(?<blockData>.*?)(\r\n|\n|\r)-{40} BLOCK (\d+) END", RegexOptions.Compiled);
            // get individual blocks from input text file
            MatchCollection blockList = blockRegex.Matches(decodedText);

            string outputDatFilePath = Path.GetDirectoryName(textFilePath) + "\\" + Path.GetFileNameWithoutExtension(textFilePath) + "_enc.dat";

            FileMode openMode;
            if (File.Exists(outputDatFilePath))
                openMode = FileMode.Truncate;
            else
                openMode = FileMode.CreateNew;

            using (BinaryWriter outFile = new BinaryWriter(File.Open(outputDatFilePath, openMode, FileAccess.ReadWrite)))
            {
                // write block count
                WriteUShort((ushort)blockList.Count, outFile);

                // write blocks
                for (int i = 0; i < blockList.Count; i++)
                {
                    string blockData = blockList[i].Groups["blockData"].Value;
                    outFile.Write(EncodeBlock(blockData, fontChars));

                }
            }

        }

        static string DecodeBlock(byte[] block, string fontChars)
        {
            StringBuilder blockText = new StringBuilder("", block.Length * 2);
            ushort value;
            ushort flagID;
            ushort flagValue;
            ushort itemID;
            ushort poseID;
            ushort mantraID;
            ushort cyan;
            ushort magenta;
            ushort yellow;
            ushort cmdLength;
            ushort cmdElement;
            ushort sceneID;
            for (int pos = 0; pos < (block.Length);)
            {
                value = ReadUShort(block, ref pos);
                // line feed, space and form feed
                if (value <= 0x0020)
                {
                    switch (value)
                    {

                        case 0x000A:
                        case 0x0020:
                            blockText.Append((char)value);
                            break;

                        case 0x000C:
                            blockText.Append("{FF}");
                            break;
                    }
                }

                // special values
                if (value >= 0x40 && value <= 0x50)
                {
                    switch (value)
                    {
                        case 0x0040:
                            flagID = ReadUShort(block, ref pos);
                            flagValue = ReadUShort(block, ref pos);
                            blockText.Append($"{{FLAG {flagID}:={flagValue}}}");
                            break;

                        case 0x0042:
                            itemID = ReadUShort(block, ref pos);
                            blockText.Append($"{{ITEM {itemID}}}");
                            break;

                        case 0x0044:
                            blockText.Append($"{{CLS}}");
                            break;

                        case 0x0045:
                            blockText.Append($"{{BR}}");
                            break;

                        case 0x0046:
                            poseID = ReadUShort(block, ref pos);
                            blockText.Append($"{{POSE {poseID}}}");
                            break;

                        case 0x0047:
                            mantraID = ReadUShort(block, ref pos);
                            blockText.Append($"{{MANTRA {mantraID}}}");
                            break;

                        case 0x004A:
                            cyan = ReadUShort(block, ref pos);
                            magenta = ReadUShort(block, ref pos);
                            yellow = ReadUShort(block, ref pos);
                            blockText.Append($"{{COL " + $"{cyan:D3}-{magenta:D3}-{yellow:D3}" + $"}}");
                            break;

                        case 0x004E:
                            cmdLength = ReadUShort(block, ref pos);
                            blockText.Append("{CMD ");
                            for (int j = 0; j < cmdLength; j++)
                            {
                                cmdElement = ReadUShort(block, ref pos);
                                blockText.Append(cmdElement.ToString());
                                if (j < cmdLength - 1)
                                {
                                    blockText.Append("-");
                                }
                            }
                            blockText.Append("}");
                            break;

                        case 0x004F:
                            sceneID = ReadUShort(block, ref pos);
                            blockText.Append($"{{SCENE {sceneID}}}");
                            break;

                        default:
                            blockText.Append($"{{UNK {value}}}");
                            blockText.Append("\n");
                            break;

                    }
                }

                // regular characters
                if (value >= 0x0100)
                {
                    int characterIndex = (int)value - 0x100;
                    blockText.Append(fontChars[characterIndex].ToString());
                }
            }

            return blockText.ToString();
        }

        static byte[] EncodeBlock(string blockText, string fontChars)
        {
            StringBuilder specialString = new StringBuilder();
            ushort charIndex;
            Regex specialRegex = new Regex(@"(?n)^{(?<command>[a-zA-Z]+)(\s+?(?<arguments>.*?))?}", RegexOptions.Compiled);
            Regex flagRegex = new Regex(@"(\d+)\s?:=\s?(\d+)", RegexOptions.Compiled);
            Regex colorRegex = new Regex(@"(\d+)-(\d+)-(\d+)", RegexOptions.Compiled);
            Regex cmdRegex = new Regex(@"(\d+)-?", RegexOptions.Compiled);

            string args = "";
            ushort flagID;
            ushort flagValue;
            ushort itemID;
            ushort poseID;
            ushort mantraID;
            ushort cyan;
            ushort magenta;
            ushort yellow;
            ushort sceneID;
            ushort cmdArg;

            MemoryStream stream = new MemoryStream();
            using (BinaryWriter blockData = new BinaryWriter(stream))
            {
                blockData.Write((ushort)0x0000);    // write placeholder for block length

                for (int i = 0; i < blockText.Length;)
                {
                    // HANDLE SPECIAL VALUE (COMMAND)
                    if (i < blockText.Length && blockText[i].Equals('{'))
                    {
                        specialString.Clear();

                        // get whole command
                        while (true)
                        {
                            specialString.Append(blockText[i]);
                            if (blockText[i].Equals('}'))
                            {
                                i++;
                                break;
                            }
                            i++;
                        }

                        // identify, parse and write to block
                        Match special = specialRegex.Match(specialString.ToString());
                        args = special.Groups["arguments"].Value;
                        switch (special.Groups["command"].Value)
                        {
                            case "FF":
                                WriteUShort(0x000C, blockData);
                                break;

                            case "FLAG":
                                Match flagData = flagRegex.Match(args);
                                flagID = UInt16.Parse(flagData.Groups[1].Value);
                                flagValue = UInt16.Parse(flagData.Groups[2].Value);
                                WriteUShort(0x0040, blockData);
                                WriteUShort(flagID, blockData);
                                WriteUShort(flagValue, blockData);
                                break;

                            case "ITEM":
                                itemID = UInt16.Parse(args);
                                WriteUShort(0x0042, blockData);
                                WriteUShort(itemID, blockData);
                                break;

                            case "CLS":
                                WriteUShort(0x0044, blockData);
                                break;

                            case "BR":
                                WriteUShort(0x0045, blockData);
                                break;

                            case "POSE":
                                poseID = UInt16.Parse(args);
                                WriteUShort(0x0046, blockData);
                                WriteUShort(poseID, blockData);
                                break;

                            case "MANTRA":
                                mantraID = UInt16.Parse(args);
                                WriteUShort(0x0047, blockData);
                                WriteUShort(mantraID, blockData);
                                break;

                            case "COL":
                                Match colorData = colorRegex.Match(args);
                                cyan = UInt16.Parse(colorData.Groups[1].Value);
                                magenta = UInt16.Parse(colorData.Groups[2].Value);
                                yellow = UInt16.Parse(colorData.Groups[3].Value);
                                WriteUShort(0x004a, blockData);
                                WriteUShort(cyan, blockData);
                                WriteUShort(magenta, blockData);
                                WriteUShort(yellow, blockData);
                                break;

                            case "CMD":
                                MatchCollection argList = cmdRegex.Matches(args);
                                WriteUShort(0x004e, blockData);
                                WriteUShort((ushort)argList.Count, blockData);
                                for (int x = 0; x < argList.Count; x++)
                                {
                                    cmdArg = UInt16.Parse(argList[x].Groups[1].Value);
                                    WriteUShort(cmdArg, blockData);
                                }
                                break;

                            case "SCENE":
                                sceneID = UInt16.Parse(args);
                                WriteUShort(0x004f, blockData);
                                WriteUShort(sceneID, blockData);
                                break;

                        }

                    }

                    // HANDLE REGULAR CHARACTERS
                    if (i < blockText.Length && !blockText[i].Equals('{'))
                    {
                        while (i < blockText.Length &&  !blockText[i].Equals('{'))
                        {
                            // the line break separate entries
                            // (read according to the OS the text file was edited on)
                            if (blockText[i].Equals('\n'))          // linux
                            {
                                WriteUShort(0x000A, blockData);
                                i++;
                            }
                            else if (blockText[i].Equals('\r'))     // mac 
                            {
                                i++;
                                if (i < blockText.Length && blockText[i].Equals('\n'))      // windows
                                {
                                    i++;
                                }
                                WriteUShort(0x000A, blockData);
                            }

                            // space
                            else if (blockText[i].Equals(' ')) 
                            {
                                WriteUShort(0x0020, blockData);
                                i++;
                            }

                            // finally, for regular characters, get index and store it
                            else
                            {
                                charIndex = (ushort)(fontChars.IndexOf(blockText[i]) + 0x100);
                                WriteUShort(charIndex, blockData);
                                i++;
                            }

                        }
                    }
                }

                // write block length
                blockData.BaseStream.Seek(0, SeekOrigin.Begin);
                WriteUShort((ushort)(blockData.BaseStream.Length - 2), blockData);
            }

            byte[] encodedBlock = stream.ToArray();
            stream.Dispose();
            return encodedBlock;
        }

        static ushort ReadUShort(byte[] input, ref int pos)
        {
            byte[] data = new byte[2];
            Array.Copy(input, pos, data, 0, 2);
            pos += 2;
            short result = BitConverter.ToInt16(data, 0);
            return (ushort)IPAddress.NetworkToHostOrder(result);    // reverse byte order if necessary
        }

        static void WriteUShort(ushort value, BinaryWriter input)
        {
            value = (ushort)IPAddress.HostToNetworkOrder((short)value);    // reverse byte order if necessary
            input.Write(value);
        }

        static bool uniqueCharacters(string str)
        {
            // from https://www.geeksforgeeks.org/determine-string-unique-characters/
            // If at any time we encounter 2
            // same characters, return false
            for (int i = 0; i < str.Length; i++)
                for (int j = i + 1; j < str.Length; j++)
                    if (str[i] == str[j])
                        return false;

            // If no duplicate characters
            // encountered, return true
            return true;
        }

    }

}
