﻿// Part of FemtoCraft | Copyright 2012 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
// Based on fCraft.MapConversion.MapMCSharp - fCraft is Copyright 2009-2012 Matvei Stefarov <me@matvei.org> | See LICENSE.fCraft.txt
using System;
using System.IO;
using System.IO.Compression;

namespace FemtoCraft {
    unsafe class Map {
        public static Map CreateFlatgrass( int width, int length, int height ) {
            Map map = new Map( width, length, height );
            map.Blocks.MemSet( (byte)Block.Stone, 0, width * length * ( height / 2 - 5 ) );
            map.Blocks.MemSet( (byte)Block.Dirt, width * length * ( height / 2 - 5 ), width * length * 4 );
            map.Blocks.MemSet( (byte)Block.Grass, width * length * ( height / 2 - 1 ), width * length );
            return map;
        }


        public readonly int Width, Length, Height, Volume;
        public readonly byte[] Blocks;
        public Position Spawn;

        public Map( int width, int length, int height ) {
            if( width < 16 || width > 2048 ||
                length < 16 || length > 2048 ||
                height < 16 || height > 2048 ) {
                throw new ArgumentException( "Invalid map dimension(s)." );
            }
            Width = width;
            Length = length;
            Height = height;
            Volume = width * length * height;
            Blocks = new byte[Volume];
            Spawn = new Position( Width * 16, Length * 16,
                                  Math.Min( Height * 32, short.MaxValue ) );
        }


        public int Index( int x, int y, int z ) {
            return ( z * Length + y ) * Width + x;
        }


        public void SetBlock( int x, int y, int z, Block type ) {
            if( x < Width && y < Length && z < Height && x >= 0 && y >= 0 && z >= 0 ) {
                Blocks[Index( x, y, z )] = (byte)type;
            }
        }


        public Block GetBlock( int x, int y, int z ) {
            if( x < Width && y < Length && z < Height && x >= 0 && y >= 0 && z >= 0 ) {
                return (Block)Blocks[Index( x, y, z )];
            } else {
                return Block.Undefined;
            }
        }


        public bool InBounds( int x, int y, int z ) {
            return x < Width && y < Length && z < Height && x >= 0 && y >= 0 && z >= 0;
        }



        public static Map Load( string fileName ) {
            using( FileStream mapStream = File.OpenRead( fileName ) ) {
                using( GZipStream gs = new GZipStream( mapStream, CompressionMode.Decompress ) ) {
                    BinaryReader bs = new BinaryReader( gs );

                    // Read in the magic number
                    if( bs.ReadUInt16() != 0x752 ) {
                        throw new Exception( "Could not load map (incorrect header)." );
                    }

                    // Read in the map dimesions
                    int width = bs.ReadInt16();
                    int length = bs.ReadInt16();
                    int height = bs.ReadInt16();

                    Map map = new Map( width, length, height );

                    // Read in the spawn location
                    map.Spawn = new Position {
                        X = (short)( bs.ReadInt16() * 32 ),
                        Z = (short)( bs.ReadInt16() * 32 ),
                        Y = (short)( bs.ReadInt16() * 32 ),
                        R = bs.ReadByte(),
                        L = bs.ReadByte(),
                    };

                    // Write the VistPermission and BuildPermission bytes
                    gs.ReadByte();
                    gs.ReadByte();

                    // Read map data
                    int bytesRead = 0;
                    int bytesLeft = map.Blocks.Length;
                    while( bytesLeft > 0 ) {
                        int readPass = gs.Read( map.Blocks, bytesRead, bytesLeft );
                        if( readPass == 0 ) throw new EndOfStreamException();
                        bytesRead += readPass;
                        bytesLeft -= readPass;
                    }
                    
                    fixed( byte* ptr = map.Blocks ) {
                        for( int j = 0; j < map.Blocks.Length; j++ ) {
                            if( ptr[j] > 49 ) {
                                ptr[j] = Mapping[ptr[j]];
                            }
                        }
                    }

                    return map;
                }
            }
        }

        public void Save( string fileName ) {
            using( FileStream mapStream = File.Create( fileName ) ) {
                using( GZipStream gs = new GZipStream( mapStream, CompressionMode.Compress ) ) {
                    BinaryWriter bs = new BinaryWriter( gs );

                    // Write the magic number
                    bs.Write( (ushort)0x752 );

                    // Write the map dimensions
                    bs.Write( (short)Width );
                    bs.Write( (short)Length );
                    bs.Write( (short)Height );

                    // Write the spawn location
                    bs.Write( (short)(Spawn.X / 32) );
                    bs.Write( (short)(Spawn.Z / 32) );
                    bs.Write( (short)(Spawn.Y / 32) );

                    // Write the spawn orientation
                    bs.Write( Spawn.R );
                    bs.Write( Spawn.L );

                    // Write the VistPermission and BuildPermission bytes
                    bs.Write( (byte)0 );
                    bs.Write( (byte)0 );

                    // Write the map data
                    bs.Write( Blocks, 0, Volume );

                    bs.Close();
                }
            }
        }


        
        static readonly byte[] Mapping = new byte[256];

        static Map() {
            Mapping[100] = (byte)Block.Glass;       // op_glass
            Mapping[101] = (byte)Block.Obsidian;    // opsidian
            Mapping[102] = (byte)Block.Brick;       // op_brick
            Mapping[103] = (byte)Block.Stone;       // op_stone
            Mapping[104] = (byte)Block.Cobblestone;       // op_cobblestone
            // 105 = op_air
            Mapping[106] = (byte)Block.Water;       // op_water

            // 107-109 unused
            Mapping[110] = (byte)Block.Wood;        // wood_float
            Mapping[111] = (byte)Block.Log;         // door
            Mapping[112] = (byte)Block.Lava;        // lava_fast
            Mapping[113] = (byte)Block.Obsidian;    // door2
            Mapping[114] = (byte)Block.Glass;       // door3
            Mapping[115] = (byte)Block.Stone;       // door4
            Mapping[116] = (byte)Block.Leaves;      // door5
            Mapping[117] = (byte)Block.Sand;        // door6
            Mapping[118] = (byte)Block.Wood;        // door7
            Mapping[119] = (byte)Block.Green;       // door8
            Mapping[120] = (byte)Block.TNT;         // door9
            Mapping[121] = (byte)Block.Stair;       // door10

            Mapping[122] = (byte)Block.Log;         // tdoor
            Mapping[123] = (byte)Block.Obsidian;    // tdoor2
            Mapping[124] = (byte)Block.Glass;       // tdoor3
            Mapping[125] = (byte)Block.Stone;       // tdoor4
            Mapping[126] = (byte)Block.Leaves;      // tdoor5
            Mapping[127] = (byte)Block.Sand;        // tdoor6
            Mapping[128] = (byte)Block.Wood;        // tdoor7
            Mapping[129] = (byte)Block.Green;       // tdoor8

            Mapping[130] = (byte)Block.White;       // MsgWhite
            Mapping[131] = (byte)Block.Black;       // MsgBlack
            Mapping[132] = (byte)Block.Air;         // MsgAir
            Mapping[133] = (byte)Block.Water;       // MsgWater
            Mapping[134] = (byte)Block.Lava;        // MsgLava

            Mapping[135] = (byte)Block.TNT;         // tdoor9
            Mapping[136] = (byte)Block.Stair;       // tdoor10
            Mapping[137] = (byte)Block.Air;         // tdoor11
            Mapping[138] = (byte)Block.Water;       // tdoor12
            Mapping[139] = (byte)Block.Lava;        // tdoor13

            Mapping[140] = (byte)Block.Water;       // WaterDown
            Mapping[141] = (byte)Block.Lava;        // LavaDown
            Mapping[143] = (byte)Block.Aqua;        // WaterFaucet
            Mapping[144] = (byte)Block.Orange;      // LavaFaucet

            // 143 unused
            Mapping[145] = (byte)Block.Water;       // finiteWater
            Mapping[146] = (byte)Block.Lava;        // finiteLava
            Mapping[147] = (byte)Block.Cyan;        // finiteFaucet

            Mapping[148] = (byte)Block.Log;         // odoor1
            Mapping[149] = (byte)Block.Obsidian;    // odoor2
            Mapping[150] = (byte)Block.Glass;       // odoor3
            Mapping[151] = (byte)Block.Stone;       // odoor4
            Mapping[152] = (byte)Block.Leaves;      // odoor5
            Mapping[153] = (byte)Block.Sand;        // odoor6
            Mapping[154] = (byte)Block.Wood;        // odoor7
            Mapping[155] = (byte)Block.Green;       // odoor8
            Mapping[156] = (byte)Block.TNT;         // odoor9
            Mapping[157] = (byte)Block.Stair;       // odoor10
            Mapping[158] = (byte)Block.Lava;        // odoor11
            Mapping[159] = (byte)Block.Water;       // odoor12

            Mapping[160] = (byte)Block.Air;         // air_portal
            Mapping[161] = (byte)Block.Water;       // water_portal
            Mapping[162] = (byte)Block.Lava;        // lava_portal

            // 163 unused
            Mapping[164] = (byte)Block.Air;         // air_door
            Mapping[165] = (byte)Block.Air;         // air_switch
            Mapping[166] = (byte)Block.Water;       // water_door
            Mapping[167] = (byte)Block.Lava;        // lava_door

            // 168-174 = odoor*_air
            Mapping[175] = (byte)Block.Cyan;        // blue_portal
            Mapping[176] = (byte)Block.Orange;      // orange_portal
            // 177-181 = odoor*_air

            Mapping[182] = (byte)Block.TNT;         // smalltnt
            Mapping[183] = (byte)Block.TNT;         // bigtnt
            Mapping[184] = (byte)Block.Lava;        // tntexplosion
            Mapping[185] = (byte)Block.Lava;        // fire

            // 186 unused
            Mapping[187] = (byte)Block.Glass;       // rocketstart
            Mapping[188] = (byte)Block.Gold;        // rockethead
            Mapping[189] = (byte)Block.Iron;       // firework

            Mapping[190] = (byte)Block.Lava;        // deathlava
            Mapping[191] = (byte)Block.Water;       // deathwater
            Mapping[192] = (byte)Block.Air;         // deathair
            Mapping[193] = (byte)Block.Water;       // activedeathwater
            Mapping[194] = (byte)Block.Lava;        // activedeathlava

            Mapping[195] = (byte)Block.Lava;        // magma
            Mapping[196] = (byte)Block.Water;       // geyser

            // 197-210 = air
            Mapping[211] = (byte)Block.Red;         // door8_air
            Mapping[212] = (byte)Block.Lava;        // door9_air
            // 213-229 = air

            Mapping[230] = (byte)Block.Aqua;        // train
            Mapping[231] = (byte)Block.TNT;         // creeper
            Mapping[232] = (byte)Block.MossyRocks;  // zombiebody
            Mapping[233] = (byte)Block.Lime;        // zombiehead

            // 234 unused
            Mapping[235] = (byte)Block.White;       // birdwhite
            Mapping[236] = (byte)Block.Black;       // birdblack
            Mapping[237] = (byte)Block.Lava;        // birdlava
            Mapping[238] = (byte)Block.Red;         // birdred
            Mapping[239] = (byte)Block.Water;       // birdwater
            Mapping[240] = (byte)Block.Blue;        // birdblue
            Mapping[242] = (byte)Block.Lava;        // birdkill

            Mapping[245] = (byte)Block.Gold;        // fishgold
            Mapping[246] = (byte)Block.Sponge;      // fishsponge
            Mapping[247] = (byte)Block.Gray;        // fishshark
            Mapping[248] = (byte)Block.Red;         // fishsalmon
            Mapping[249] = (byte)Block.Blue;        // fishbetta
        }
    }
}
