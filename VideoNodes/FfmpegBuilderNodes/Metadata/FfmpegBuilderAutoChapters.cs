﻿using FileFlows.VideoNodes.FfmpegBuilderNodes.Models;
using System.Text;

namespace FileFlows.VideoNodes.FfmpegBuilderNodes
{
    public class FfmpegBuilderAutoChapters : FfmpegBuilderNode
    {
        public override int Outputs => 2;
        public override string HelpUrl => "https://fileflows.com/docs/plugins/video-nodes/ffmpeg-builder/auto-chapters";

        [NumberInt(1)]
        [DefaultValue(60)]
        public int MinimumLength { get; set; } = 60;

        [NumberInt(2)]
        [DefaultValue(45)]
        public int Percent { get; set; } = 45;

        public override int Execute(NodeParameters args)
        {
            VideoInfo videoInfo = GetVideoInfo(args);
            if (videoInfo == null)
                return -1;

            if (videoInfo.Chapters?.Count > 3)
            {
                args.Logger.ILog(videoInfo.Chapters.Count + " chapters already detected in file");
                return 2;
            }

            var lfResult = args.FileService.GetLocalPath(args.WorkingFile);
            if (lfResult.Failed(out var error))
            {
                args.Logger?.WLog("Failed to get local file: " + error);
                return 2;
            }
            

            string tempMetaDataFile = GenerateMetaDataFile(this, args, videoInfo, lfResult.Value, FFMPEG, this.Percent, this.MinimumLength);
            if (string.IsNullOrEmpty(tempMetaDataFile))
                return 2;

            Model.InputFiles.Add(new InputFile(tempMetaDataFile));
            Model.MetadataParameters.AddRange(new[] { "-map_metadata", (Model.InputFiles.Count - 1).ToString() });
            return 1;
        }

        string GenerateMetaDataFile(EncodingNode node, NodeParameters args, VideoInfo videoInfo, string localFile, string ffmpegExe, int percent, int minimumLength)
        {
            string output;
            var result = node.Encode(args, ffmpegExe, new List<string>
            {
                "-hide_banner",
                "-i", localFile,
                "-filter:v", $"select='gt(scene,{percent / 100f})',showinfo",
                "-f", "null",
                "-"
            }, out output, updateWorkingFile: false, dontAddInputFile: true);

            if (result == false)
            {
                args.Logger?.WLog("Failed to detect scenes");
                return string.Empty;
            }

            if (minimumLength < 30)
            {
                args.Logger?.ILog("Minimum length set to invalid number, defaulting to 60 seconds");
                minimumLength = 60;
            }
            else
            {
                args.Logger?.ILog($"Minimum length of chapter {minimumLength} seconds");
            }

            StringBuilder metadata = new StringBuilder();
            metadata.AppendLine(";FFMETADATA1");
            metadata.AppendLine("");

            int chapter = 0;

            TimeSpan previous = TimeSpan.Zero;
            foreach (Match match in Regex.Matches(output, @"(?<=(pts_time:))[\d]+\.[\d]+"))
            {
                TimeSpan time = TimeSpan.FromSeconds(double.Parse(match.Value));
                if (Math.Abs((time - previous).TotalSeconds) < minimumLength)
                    continue;

                AddChapter(previous, time);
                previous = time;
            }

            var totalTime = TimeSpan.FromSeconds(videoInfo.VideoStreams[0].Duration.TotalSeconds);
            if (Math.Abs((totalTime - previous).TotalSeconds) > minimumLength)
                AddChapter(previous, totalTime);

            if (chapter == 0)
            {
                args.Logger?.ILog("Failed to detect any scene changes");
                return string.Empty;
            }

            string tempMetaDataFile = System.IO.Path.Combine(args.TempPath, Guid.NewGuid() + ".txt");
            System.IO.File.WriteAllText(tempMetaDataFile, metadata.ToString());

            return tempMetaDataFile;

            void AddChapter(TimeSpan start, TimeSpan end)
            {

                metadata.AppendLine("[CHAPTER]");
                metadata.AppendLine("TIMEBASE=1/1000");
                metadata.AppendLine("START=" + ((int)(start.TotalSeconds * 1000)));
                metadata.AppendLine("END=" + ((int)(end.TotalSeconds * 1000)));
                metadata.AppendLine("title=Chapter " + (++chapter));
                metadata.AppendLine();
            }
        }
    }
}
