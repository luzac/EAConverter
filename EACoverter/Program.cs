using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LitJson;

namespace EACoverter {
    public class FilterConfig {
        public string SaveFileName;
        public List<string> FilterList;
        public bool NeedToRemove = false;
        public PredictionType PType = PredictionType.ENROUT;
        public bool IsPDF = true;
    }

    public class EAConfig {
        public List<string> DeleteList;
        public List<FilterConfig> FilterList;
    }

    public enum PredictionType {
        ENROUT,
        AIRPORT
    }

    class Prediction {
        public PredictionType PType = PredictionType.ENROUT;
        public string KeyVal = string.Empty;
        private List<string> content = null;
        public List<string> Content {
            get { return content; }
            set {
                content = value;
            }
        }

        public List<int> RNPList = new List<int>();
    }

    class Program {

        public static List<Prediction> Predictions = new List<Prediction>();
        public static DirectoryInfo ResultDirectoryInfo = null;
        private static EAConfig MyConfig = null;

        static void Main(string[] args) {
            LoadData();
            Console.ReadLine();
        }

        static void LoadData() {
            Predictions.Clear();

            string resultFolder = Directory.GetCurrentDirectory() + "\\Result";
            if (!Directory.Exists(resultFolder))
                Directory.CreateDirectory(resultFolder);
            ResultDirectoryInfo = new DirectoryInfo(resultFolder);
            ResultDirectoryInfo.GetFiles().ToList().ForEach(file => file.Delete());

            string tmpFolder = Directory.GetCurrentDirectory() + "\\Tmp";
            if (Directory.Exists(tmpFolder))
                Directory.Delete(tmpFolder, true);

            DirectoryInfo currentDirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            List<FileInfo> files = currentDirInfo.GetFiles().ToList();
            FileInfo firstValidZipFile = files.FirstOrDefault(fileInfo => fileInfo.Name.EndsWith(".zip"));
            if (firstValidZipFile == null) {
                Console.WriteLine("No valid zip is found!");
                return;
            }

            ZipFile.ExtractToDirectory(firstValidZipFile.Name, tmpFolder);
            DirectoryInfo tmpDirectory = new DirectoryInfo(tmpFolder);
            files = tmpDirectory.GetFiles().ToList();
            files.ForEach(fileInfo => {
                if (fileInfo.Name.EndsWith(".txt")) {
                    Console.WriteLine(fileInfo.Name);
                    ProcessFile(fileInfo);
                }
            });
            Console.WriteLine("TotalFileCount:" + files.Count);
            Console.WriteLine("ValidFileCount:" + Predictions.Count);

            string cfgFilePath = Directory.GetCurrentDirectory() + "\\EAConfig.cfg";
            if (!File.Exists(cfgFilePath)) {
                Console.WriteLine("EAConfig.cfg not found!");
                return;
            }
            else {
                DirectoryInfo cfgFileInfo = new DirectoryInfo(cfgFilePath);
                StreamReader sR = new StreamReader(cfgFileInfo.FullName);
                string content = string.Empty;
                string line = string.Empty;
                while ((line = sR.ReadLine()) != null) {
                    Console.WriteLine(line);
                    content += line;
                }
                sR.Close();
                MyConfig = JsonMapper.ToObject<EAConfig>(content);
            }
            DoFilter();
        }

        static void ProcessFile(FileInfo validFile) {
            StreamReader sR = new StreamReader(validFile.FullName);
            List<string> content = new List<string>();
            List<int> RNPList = new List<int>();
            string line = string.Empty;
            while ((line = sR.ReadLine()) != null) {
                Console.WriteLine(line);
                content.Add(line);
                if (line.StartsWith("RNP")) {
                    int colonIndex = line.IndexOf(":");
                    string tmpString = line.Substring(3, colonIndex - 3);
                    float rnpStandardVal = float.MinValue;
                    if (float.TryParse(tmpString, out rnpStandardVal))
                        RNPList.Add((int)rnpStandardVal);
                }
            }
            sR.Close();

            if (content.Count == 0) {
                Console.WriteLine("Prediction file is empty:{0}", validFile.Name);
                return;
            }

            string keyString = content.FirstOrDefault(str => {
                return str.Contains("ENROUT") || str.Contains("AIRPORT");
            });

            if (string.IsNullOrEmpty(keyString)) {
                Console.WriteLine("Prediction file has no key(ENROUT | AIRPORT):{0}", validFile.Name);
                return;
            }

            string[] result = keyString.Split(':');
            if (result == null || result.Length != 2) {
                Console.WriteLine("Prediction file's key has format error:{0}", validFile.Name);
                return;
            }

            Prediction newPrediction = new Prediction();
            newPrediction.PType = result[0].Trim() == "ENROUT" ? PredictionType.ENROUT : PredictionType.AIRPORT;
            newPrediction.KeyVal = result[1].Trim();
            newPrediction.Content = content;
            newPrediction.RNPList = RNPList;
            Predictions.Add(newPrediction);
        }

        static void DoFilter() {
            Console.WriteLine(JsonMapper.ToJson(MyConfig));
            DoRemove(MyConfig.DeleteList);
            //GentBriefView(true);
            //GentBriefView(false);
            MyConfig.FilterList.ForEach(filter => {
                DoFilter(filter.FilterList, filter.SaveFileName, filter.PType, filter.NeedToRemove, filter.IsPDF);
            });
            Console.WriteLine("Convert Finished!");
            //DoRemove(new List<string> { "L642", "M771", "Q1", "Q15" });
            //DoFilter(new List<string> { "A345", "B451" }, "ZBLA", PredictionType.ENROUT, true);
            //DoFilter(new List<string> { "H89", "H90", "H117", "W146", "W162" }, "ZPLJ", PredictionType.ENROUT, true);
            //DoFilter(new List<string> { "M503" }, "M503", PredictionType.ENROUT, true);
            //DoFilter(new List<string> { "B215" }, "B215", PredictionType.ENROUT, false);
            //DoFilter(new List<string>(), "ZWWW", PredictionType.ENROUT, true);
            //DoFilter(new List<string>(), "RAIM", PredictionType.AIRPORT, true);
        }

        private static void GentBriefView(bool isAirport) {
            //Present all airport prediction
            PredictionType targetType = isAirport ? PredictionType.AIRPORT : PredictionType.ENROUT;
            string targetFileName = isAirport ? "AirportBriefView.pdf" : "EnroutBriefView.pdf";
            var allValidPredicts = Predictions.Where(pred => pred.PType == targetType);
            List<Prediction> predicts_EX = new List<Prediction>();
            List<Prediction> predicts_Normal = new List<Prediction>();
            allValidPredicts.ToList().ForEach(pred => {
                if (pred.Content.Any(line => line.Contains("except for")))
                    predicts_EX.Add(pred);
                else
                    predicts_Normal.Add(pred);
            });

            TxtToPDFConverter.GenerateBriefView(isAirport, predicts_EX, predicts_Normal,
                string.Format("{0}\\{1}", ResultDirectoryInfo.FullName, targetFileName));
        }

        static void DoFilter(List<string> keys, string targetFileName, PredictionType targetType, bool needToRemove, bool isPDF) {
            Debug.Assert(keys != null);
            List<Prediction> validPreds = new List<Prediction>();

            if (keys.Count == 0)
                validPreds.AddRange(Predictions.Where(pred => pred.PType == targetType));
            else
                validPreds = Predictions.Where(pred => keys.Contains(pred.KeyVal)).ToList();

            if (validPreds.Count == 0)
                return;

            //TwoImplements
            if (ResultDirectoryInfo != null && !string.IsNullOrEmpty(targetFileName)) {
                if (targetType == PredictionType.AIRPORT) {
                    if (!isPDF) {
                        string targetName = string.Format("{0}\\{1}.txt", ResultDirectoryInfo.FullName, targetFileName);
                        StreamWriter sw = new StreamWriter(targetName);
                        validPreds.ForEach(pred => {
                            pred.Content.ForEach(singleContent => sw.WriteLine(singleContent));
                            sw.WriteLine("");
                        });
                        sw.Close();
                        Console.WriteLine(string.Format("{0}.txt is generated!", targetName));
                    }
                    else {
                        string targetName = string.Format("{0}\\{1}.pdf", ResultDirectoryInfo.FullName, targetFileName);
                        TxtToPDFConverter.Convert(validPreds, targetName);
                    }
                }
                else {
                    var allValidPredicts = validPreds.Where(pred => pred.PType == targetType);
                    List<Prediction> predicts_EX = new List<Prediction>();
                    List<Prediction> predicts_Normal = new List<Prediction>();
                    allValidPredicts.ToList().ForEach(pred => {
                        if (pred.Content.Any(line => line.Contains("except for")))
                            predicts_EX.Add(pred);
                        else
                            predicts_Normal.Add(pred);
                    });

                    if (!isPDF) {
                        var result = TxtToPDFConverter.GenerateBriefContents(false, predicts_EX, predicts_Normal);
                        string targetName = string.Format("{0}\\{1}.txt", ResultDirectoryInfo.FullName, targetFileName);
                        StreamWriter sw = new StreamWriter(targetName);
                        result.ForEach(line => {
                            sw.WriteLine(line);
                        });
                        sw.Close();
                        Console.WriteLine(string.Format("{0}.txt is generated!", targetName));
                    }
                    else {
                        string targetName = string.Format("{0}\\{1}.pdf", ResultDirectoryInfo.FullName, targetFileName);
                        TxtToPDFConverter.GenerateBriefView(false, predicts_EX, predicts_Normal, targetName);
                    }
                }
            }
            if (needToRemove)
                validPreds.ForEach(pred => Predictions.Remove(pred));
            Console.WriteLine("Prediction Count Left:" + Predictions.Count);
        }

        static void DoRemove(List<string> keys) {
            if (keys == null)
                return;
            Predicate<Prediction> toRemoveCondition = pred => { return keys.Contains(pred.KeyVal); };
            Predictions.RemoveAll(toRemoveCondition);
        }
    }
}