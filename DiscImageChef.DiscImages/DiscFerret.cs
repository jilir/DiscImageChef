﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : DiscFerret.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Disk image plugins.
//
// --[ Description ] ----------------------------------------------------------
//
//     Manages DiscFerret flux images.
//
// --[ License ] --------------------------------------------------------------
//
//     This library is free software; you can redistribute it and/or modify
//     it under the terms of the GNU Lesser General Public License as
//     published by the Free Software Foundation; either version 2.1 of the
//     License, or (at your option) any later version.
//
//     This library is distributed in the hope that it will be useful, but
//     WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//     Lesser General Public License for more details.
//
//     You should have received a copy of the GNU Lesser General Public
//     License along with this library; if not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2018 Natalia Portillo
// ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DiscImageChef.CommonTypes;
using DiscImageChef.Console;
using DiscImageChef.Filters;

namespace DiscImageChef.DiscImages
{
    public class DiscFerret : ImagePlugin
    {
        /// <summary>
        ///     "DFER"
        /// </summary>
        const uint DFI_MAGIC = 0x52454644;
        /// <summary>
        ///     "DFE2"
        /// </summary>
        const uint DFI_MAGIC2 = 0x32454644;
        // TODO: These variables have been made public so create-sidecar can access to this information until I define an API >4.0
        public SortedDictionary<int, long> TrackLengths;
        public SortedDictionary<int, long> TrackOffsets;

        public DiscFerret()
        {
            Name = "DiscFerret";
            PluginUuid = new Guid("70EA7B9B-5323-42EB-9B40-8DDA37C5EB4D");
            ImageInfo = new ImageInfo
            {
                ReadableSectorTags = new List<SectorTagType>(),
                ReadableMediaTags = new List<MediaTagType>(),
                HasPartitions = false,
                HasSessions = false,
                Version = null,
                Application = null,
                ApplicationVersion = null,
                Creator = null,
                Comments = null,
                MediaManufacturer = null,
                MediaModel = null,
                MediaSerialNumber = null,
                MediaBarcode = null,
                MediaPartNumber = null,
                MediaSequence = 0,
                LastMediaSequence = 0,
                DriveManufacturer = null,
                DriveModel = null,
                DriveSerialNumber = null,
                DriveFirmwareRevision = null
            };
        }

        public override string ImageFormat => "DiscFerret";

        public override List<Partition> Partitions =>
            throw new FeatureUnsupportedImageException("Feature not supported by image format");

        public override List<Track> Tracks =>
            throw new FeatureUnsupportedImageException("Feature not supported by image format");

        public override List<Session> Sessions =>
            throw new FeatureUnsupportedImageException("Feature not supported by image format");

        public override bool IdentifyImage(Filter imageFilter)
        {
            byte[] magicB = new byte[4];
            Stream stream = imageFilter.GetDataForkStream();
            stream.Read(magicB, 0, 4);
            uint magic = BitConverter.ToUInt32(magicB, 0);

            return magic == DFI_MAGIC || magic == DFI_MAGIC2;
        }

        public override bool OpenImage(Filter imageFilter)
        {
            byte[] magicB = new byte[4];
            Stream stream = imageFilter.GetDataForkStream();
            stream.Read(magicB, 0, 4);
            uint magic = BitConverter.ToUInt32(magicB, 0);

            if(magic != DFI_MAGIC && magic != DFI_MAGIC2) return false;

            TrackOffsets = new SortedDictionary<int, long>();
            TrackLengths = new SortedDictionary<int, long>();
            int t = -1;
            ushort lastCylinder = 0, lastHead = 0;
            long offset = 0;

            while(stream.Position < stream.Length)
            {
                long thisOffset = stream.Position;

                DfiBlockHeader blockHeader = new DfiBlockHeader();
                byte[] blk = new byte[Marshal.SizeOf(blockHeader)];
                stream.Read(blk, 0, Marshal.SizeOf(blockHeader));
                blockHeader = BigEndianMarshal.ByteArrayToStructureBigEndian<DfiBlockHeader>(blk);

                DicConsole.DebugWriteLine("DiscFerret plugin", "block@{0}.cylinder = {1}", thisOffset,
                                          blockHeader.cylinder);
                DicConsole.DebugWriteLine("DiscFerret plugin", "block@{0}.head = {1}", thisOffset, blockHeader.head);
                DicConsole.DebugWriteLine("DiscFerret plugin", "block@{0}.sector = {1}", thisOffset,
                                          blockHeader.sector);
                DicConsole.DebugWriteLine("DiscFerret plugin", "block@{0}.length = {1}", thisOffset,
                                          blockHeader.length);

                if(stream.Position + blockHeader.length > stream.Length)
                {
                    DicConsole.DebugWriteLine("DiscFerret plugin", "Invalid track block found at {0}", thisOffset);
                    break;
                }

                stream.Position += blockHeader.length;

                if(blockHeader.cylinder > 0 && blockHeader.cylinder > lastCylinder)
                {
                    lastCylinder = blockHeader.cylinder;
                    lastHead = 0;
                    TrackOffsets.Add(t, offset);
                    TrackLengths.Add(t, thisOffset - offset + 1);
                    offset = thisOffset;
                    t++;
                }
                else if(blockHeader.head > 0 && blockHeader.head > lastHead)
                {
                    lastHead = blockHeader.head;
                    TrackOffsets.Add(t, offset);
                    TrackLengths.Add(t, thisOffset - offset + 1);
                    offset = thisOffset;
                    t++;
                }

                if(blockHeader.cylinder > ImageInfo.Cylinders) ImageInfo.Cylinders = blockHeader.cylinder;
                if(blockHeader.head > ImageInfo.Heads) ImageInfo.Heads = blockHeader.head;
            }

            ImageInfo.Heads++;
            ImageInfo.Cylinders++;

            ImageInfo.Application = "DiscFerret";
            ImageInfo.ApplicationVersion = magic == DFI_MAGIC2 ? "2.0" : "1.0";

            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadDiskTag(MediaTagType tag)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadSector(ulong sectorAddress)
        {
            return ReadSectors(sectorAddress, 1);
        }

        public override byte[] ReadSectorTag(ulong sectorAddress, SectorTagType tag)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadSectors(ulong sectorAddress, uint length)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadSectorsTag(ulong sectorAddress, uint length, SectorTagType tag)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadSectorLong(ulong sectorAddress)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadSectorLong(ulong sectorAddress, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsLong(ulong sectorAddress, uint length)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override bool? VerifySector(ulong sectorAddress)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override bool? VerifySectors(ulong sectorAddress, uint length, out List<ulong> failingLbas,
                                            out List<ulong> unknownLbas)
        {
            throw new NotImplementedException("Flux decoding is not yet implemented.");
        }

        public override byte[] ReadSector(ulong sectorAddress, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorTag(ulong sectorAddress, uint track, SectorTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectors(ulong sectorAddress, uint length, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsTag(ulong sectorAddress, uint length, uint track, SectorTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsLong(ulong sectorAddress, uint length, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override List<Track> GetSessionTracks(Session session)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override List<Track> GetSessionTracks(ushort session)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override bool? VerifySector(ulong sectorAddress, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override bool? VerifySectors(ulong sectorAddress, uint length, uint track, out List<ulong> failingLbas,
                                            out List<ulong> unknownLbas)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override bool? VerifyMediaImage()
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct DfiBlockHeader
        {
            public ushort cylinder;
            public ushort head;
            public ushort sector;
            public uint length;
        }
    }
}