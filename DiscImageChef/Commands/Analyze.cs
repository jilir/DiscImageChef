// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : Analyze.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Verbs.
//
// --[ Description ] ----------------------------------------------------------
//
//     Implements the 'analyze' verb.
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2018 Natalia Portillo
// ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using DiscImageChef.CommonTypes;
using DiscImageChef.Console;
using DiscImageChef.Core;
using DiscImageChef.DiscImages;
using DiscImageChef.Filesystems;
using DiscImageChef.Filters;

namespace DiscImageChef.Commands
{
    static class Analyze
    {
        internal static void DoAnalyze(AnalyzeOptions options)
        {
            DicConsole.DebugWriteLine("Analyze command", "--debug={0}", options.Debug);
            DicConsole.DebugWriteLine("Analyze command", "--verbose={0}", options.Verbose);
            DicConsole.DebugWriteLine("Analyze command", "--input={0}", options.InputFile);
            DicConsole.DebugWriteLine("Analyze command", "--filesystems={0}", options.SearchForFilesystems);
            DicConsole.DebugWriteLine("Analyze command", "--partitions={0}", options.SearchForPartitions);
            DicConsole.DebugWriteLine("Analyze command", "--encoding={0}", options.EncodingName);

            FiltersList filtersList = new FiltersList();
            IFilter inputFilter = filtersList.GetFilter(options.InputFile);

            if(inputFilter == null)
            {
                DicConsole.ErrorWriteLine("Cannot open specified file.");
                return;
            }

            Encoding encoding = null;

            if(options.EncodingName != null)
                try
                {
                    encoding = Claunia.Encoding.Encoding.GetEncoding(options.EncodingName);
                    if(options.Verbose) DicConsole.VerboseWriteLine("Using encoding for {0}.", encoding.EncodingName);
                }
                catch(ArgumentException)
                {
                    DicConsole.ErrorWriteLine("Specified encoding is not supported.");
                    return;
                }

            PluginBase plugins = new PluginBase();

            bool checkraw = false;

            try
            {
                IMediaImage imageFormat = ImageFormat.Detect(inputFilter);

                if(imageFormat == null)
                {
                    DicConsole.WriteLine("Image format not identified, not proceeding with analysis.");
                    return;
                }

                if(options.Verbose)
                    DicConsole.VerboseWriteLine("Image format identified by {0} ({1}).", imageFormat.Name,
                                                imageFormat.Id);
                else DicConsole.WriteLine("Image format identified by {0}.", imageFormat.Name);
                DicConsole.WriteLine();

                try
                {
                    if(!imageFormat.Open(inputFilter))
                    {
                        DicConsole.WriteLine("Unable to open image format");
                        DicConsole.WriteLine("No error given");
                        return;
                    }

                    if(options.Verbose)
                    {
                        Core.ImageInfo.PrintImageInfo(imageFormat);
                        DicConsole.WriteLine();
                    }

                    Core.Statistics.AddMediaFormat(imageFormat.Format);
                    Core.Statistics.AddMedia(imageFormat.Info.MediaType, false);
                    Core.Statistics.AddFilter(inputFilter.Name);
                }
                catch(Exception ex)
                {
                    DicConsole.ErrorWriteLine("Unable to open image format");
                    DicConsole.ErrorWriteLine("Error: {0}", ex.Message);
                    DicConsole.DebugWriteLine("Analyze command", "Stack trace: {0}", ex.StackTrace);
                    return;
                }

                List<string> idPlugins;
                IFilesystem plugin;
                string information;
                if(options.SearchForPartitions)
                {
                    List<Partition> partitions = Core.Partitions.GetAll(imageFormat);
                    Core.Partitions.AddSchemesToStats(partitions);

                    if(partitions.Count == 0)
                    {
                        DicConsole.DebugWriteLine("Analyze command", "No partitions found");
                        if(!options.SearchForFilesystems)
                        {
                            DicConsole.WriteLine("No partitions founds, not searching for filesystems");
                            return;
                        }

                        checkraw = true;
                    }
                    else
                    {
                        DicConsole.WriteLine("{0} partitions found.", partitions.Count);

                        for(int i = 0; i < partitions.Count; i++)
                        {
                            DicConsole.WriteLine();
                            DicConsole.WriteLine("Partition {0}:", partitions[i].Sequence);
                            DicConsole.WriteLine("Partition name: {0}", partitions[i].Name);
                            DicConsole.WriteLine("Partition type: {0}", partitions[i].Type);
                            DicConsole.WriteLine("Partition start: sector {0}, byte {1}", partitions[i].Start,
                                                 partitions[i].Offset);
                            DicConsole.WriteLine("Partition length: {0} sectors, {1} bytes", partitions[i].Length,
                                                 partitions[i].Size);
                            DicConsole.WriteLine("Partition scheme: {0}", partitions[i].Scheme);
                            DicConsole.WriteLine("Partition description:");
                            DicConsole.WriteLine(partitions[i].Description);

                            if(!options.SearchForFilesystems) continue;

                            DicConsole.WriteLine("Identifying filesystem on partition");

                            Core.Filesystems.Identify(imageFormat, out idPlugins, partitions[i]);
                            if(idPlugins.Count == 0) DicConsole.WriteLine("Filesystem not identified");
                            else if(idPlugins.Count > 1)
                            {
                                DicConsole.WriteLine($"Identified by {idPlugins.Count} plugins");

                                foreach(string pluginName in idPlugins)
                                    if(plugins.PluginsList.TryGetValue(pluginName, out plugin))
                                    {
                                        DicConsole.WriteLine($"As identified by {plugin.Name}.");
                                        plugin.GetInformation(imageFormat, partitions[i], out information, encoding);
                                        DicConsole.Write(information);
                                        Core.Statistics.AddFilesystem(plugin.XmlFsType.Type);
                                    }
                            }
                            else
                            {
                                plugins.PluginsList.TryGetValue(idPlugins[0], out plugin);
                                if(plugin == null) continue;

                                DicConsole.WriteLine($"Identified by {plugin.Name}.");
                                plugin.GetInformation(imageFormat, partitions[i], out information, encoding);
                                DicConsole.Write(information);
                                Core.Statistics.AddFilesystem(plugin.XmlFsType.Type);
                            }
                        }
                    }
                }

                if(checkraw)
                {
                    Partition wholePart = new Partition
                    {
                        Name = "Whole device",
                        Length = imageFormat.Info.Sectors,
                        Size = imageFormat.Info.Sectors * imageFormat.Info.SectorSize
                    };

                    Core.Filesystems.Identify(imageFormat, out idPlugins, wholePart);
                    if(idPlugins.Count == 0) DicConsole.WriteLine("Filesystem not identified");
                    else if(idPlugins.Count > 1)
                    {
                        DicConsole.WriteLine($"Identified by {idPlugins.Count} plugins");

                        foreach(string pluginName in idPlugins)
                            if(plugins.PluginsList.TryGetValue(pluginName, out plugin))
                            {
                                DicConsole.WriteLine($"As identified by {plugin.Name}.");
                                plugin.GetInformation(imageFormat, wholePart, out information, encoding);
                                DicConsole.Write(information);
                                Core.Statistics.AddFilesystem(plugin.XmlFsType.Type);
                            }
                    }
                    else
                    {
                        plugins.PluginsList.TryGetValue(idPlugins[0], out plugin);
                        if(plugin != null)
                        {
                            DicConsole.WriteLine($"Identified by {plugin.Name}.");
                            plugin.GetInformation(imageFormat, wholePart, out information, encoding);
                            DicConsole.Write(information);
                            Core.Statistics.AddFilesystem(plugin.XmlFsType.Type);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                DicConsole.ErrorWriteLine($"Error reading file: {ex.Message}");
                DicConsole.DebugWriteLine("Analyze command", ex.StackTrace);
            }

            Core.Statistics.AddCommand("analyze");
        }
    }
}