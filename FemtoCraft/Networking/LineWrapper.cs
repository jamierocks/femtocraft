﻿// Part of FemtoCraft | Copyright 2012 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
// Based on fCraft.LineWrapper - fCraft is Copyright 2009-2012 Matvei Stefarov <me@matvei.org> | See LICENSE.fCraft.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

namespace FemtoCraft {
    sealed class LineWrapper : IEnumerable<Packet>, IEnumerator<Packet> {
        const string DefaultPrefixString = "> ";
        static readonly byte[] DefaultPrefix;

        static LineWrapper() {
            DefaultPrefix = Encoding.ASCII.GetBytes( DefaultPrefixString );
        }

        const int LineSize = 64;
        const int PacketSize = 66; // opcode + id + 64

        const byte NoColor = (byte)'f';
        bool expectingColor;
        byte color, lastColor;
        bool hadColor;

        int spaceCount, wordLength;

        readonly byte[] input;
        int inputIndex;

        byte[] output;
        int outputStart, outputIndex;

        readonly byte[] prefix;

        int wrapIndex,
            wrapOutputIndex;
        byte wrapColor;


        public LineWrapper( [NotNull] string message ) {
            if( message == null ) throw new ArgumentNullException( "message" );
            input = Encoding.ASCII.GetBytes( message );
            prefix = DefaultPrefix;
            Reset();
        }


        public void Reset() {
            color = NoColor;
            wordLength = 0;
            inputIndex = 0;
        }


        public Packet Current { get; private set; }


        public bool MoveNext() {
            if( inputIndex >= input.Length ) {
                return false;
            }
            hadColor = false;

            output = new byte[PacketSize];
            output[0] = (byte)OpCode.Message;

            outputStart = 2;
            outputIndex = outputStart;
            spaceCount = 0;
            lastColor = NoColor;
            Current = new Packet( output );

            wordLength = 0;
            wrapIndex = 0;
            wrapColor = NoColor;
            wrapOutputIndex = outputStart;
            expectingColor = false;

            // Prepend line prefix, if needed
            if( inputIndex > 0 && prefix.Length > 0 ) {
                int preBufferInputIndex = inputIndex;
                byte preBufferColor = color;
                color = NoColor;
                inputIndex = 0;
                while( inputIndex < prefix.Length ) {
                    byte ch = prefix[inputIndex];
                    if( ProcessChar( ch ) ) {
                        // Should never happen, since prefix is under 32 chars
                        throw new Exception( "Prefix required wrapping." );
                    }
                    inputIndex++;
                }
                inputIndex = preBufferInputIndex;
                color = preBufferColor;
                wrapColor = preBufferColor;
            }

            // Append as much of the remaining input as possible
            while( inputIndex < input.Length ) {
                byte ch = input[inputIndex];
                if( ProcessChar( ch ) ) {
                    // Line wrap is needed
                    PrepareOutput();
                    return true;
                }
                inputIndex++;
            }
            PrepareOutput();
            return true;
        }


        bool ProcessChar( byte ch ) {
            switch( ch ) {
                case (byte)' ':
                    expectingColor = false;
                    if( spaceCount == 0 ) {
                        // first space after a word, set wrapping point
                        wrapIndex = inputIndex;
                        wrapOutputIndex = outputIndex;
                        wrapColor = color;
                    }
                    spaceCount++;
                    break;

                case (byte)'&':
                    if( expectingColor ) {
                        // append "&&"
                        expectingColor = false;
                        if( !Append( ch ) ) {
                            if( wordLength < LineSize - 1 - prefix.Length ) {
                                inputIndex = wrapIndex;
                                outputIndex = wrapOutputIndex;
                                color = wrapColor;
                            }// else word is too long, dont backtrack to wrap
                            return true;
                        }
                        spaceCount = 0;
                    } else {
                        expectingColor = true;
                    }
                    break;

                case (byte)'-':
                    expectingColor = false;
                    if( !Append( ch ) ) {
                        if( wordLength < LineSize - prefix.Length ) {
                            inputIndex = wrapIndex;
                            outputIndex = wrapOutputIndex;
                            color = wrapColor;
                        }
                        return true;
                    }
                    spaceCount = 0;
                    // allow wrapping after dash
                    wrapIndex = inputIndex + 1;
                    wrapOutputIndex = outputIndex;
                    wrapColor = color;
                    break;

                case (byte)'\n':
                    inputIndex++;
                    return true;

                default:
                    if( expectingColor ) {
                        expectingColor = false;
                        if( ProcessColor( ref ch ) ) {
                            color = ch;
                            hadColor = true;
                        }// else colorcode is invalid, skip
                    } else {
                        if( spaceCount > 0 ) {
                            wrapIndex = inputIndex;
                            wrapColor = color;
                        }
                        if( !IsWordChar( ch ) ) {
                            // replace unprintable chars with '?'
                            ch = (byte)'?';
                        }
                        if( !Append( ch ) ) {
                            if( wordLength < LineSize - prefix.Length ) {
                                inputIndex = wrapIndex;
                                outputIndex = wrapOutputIndex;
                                color = wrapColor;
                            }// else word is too long, dont backtrack to wrap
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }


        void PrepareOutput() {
            for( int i = outputIndex; i < PacketSize; i++ ) {
                output[i] = (byte)' ';
            }
            for( ; outputIndex > outputStart; outputIndex-- ) {
                if( output[outputIndex - 1] == '&' ) {
                    output[outputIndex - 1] = (byte)' ';
                } else if( output[outputIndex - 1] != ' ' ) {
                    return;
                }
            }
        }


        bool Append( byte ch ) {
            // calculate the number of characters to insert
            int bytesToInsert = 1;
            if( ch == (byte)'&' ) bytesToInsert++;

            bool prependColor = ( lastColor != color || ( color == NoColor && hadColor && outputIndex == outputStart ) );

            if( prependColor ) bytesToInsert += 2;
            if( outputIndex + bytesToInsert + spaceCount > PacketSize ) {
                return false;
            }

            // append color, if changed since last word
            if( prependColor ) {
                output[outputIndex++] = (byte)'&';
                output[outputIndex++] = color;
                lastColor = color;
            }

            //int spaceCount1 = spaceCount;
            if( spaceCount > 0 && outputIndex > outputStart ) {
                // append spaces that accumulated since last word
                while( spaceCount > 0 ) {
                    output[outputIndex++] = (byte)' ';
                    spaceCount--;
                }
                wordLength = 0;
            }
            wordLength += bytesToInsert;

            // append character
            if( ch == (byte)'&' ) output[outputIndex++] = ch;
            output[outputIndex++] = ch;
            return true;
        }


        static bool IsWordChar( byte ch ) {
            return ( ch > (byte)' ' && ch <= (byte)'~' );
        }


        static bool ProcessColor( ref byte ch ) {
            if( ch >= (byte)'A' && ch <= (byte)'Z' ) {
                ch += 32;
            }
            if( ch >= (byte)'a' && ch <= (byte)'f' ||
                ch >= (byte)'0' && ch <= (byte)'9' ) {
                return true;
            }
            return false;
        }


        [NotNull]
        object IEnumerator.Current {
            get { return Current; }
        }


        public IEnumerator<Packet> GetEnumerator() {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this;
        }

        public void Dispose() { }
    }
}