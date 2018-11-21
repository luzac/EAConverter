using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EACoverter {
    class TxtToPDFConverter {
        public static void Convert(List<Prediction> preds, string targetFileName) {
            //Document document = CreateDocument();
            Document document = CreateDocument(preds);
            document.UseCmykColor = true;
            const bool unicode = true;
            const PdfFontEmbedding embedding = PdfFontEmbedding.Always;
            PdfDocumentRenderer pdfRenderer = new PdfDocumentRenderer(unicode, embedding);
            pdfRenderer.Document = document;
            pdfRenderer.RenderDocument();
            pdfRenderer.PdfDocument.Save(targetFileName);
            Process.Start(targetFileName);
            Console.WriteLine(string.Format("{0}.pdf is generated!", targetFileName));
        }

        public static List<string> GenerateBriefContents(bool isAirport, List<Prediction> predsEx, List<Prediction> predsNormal) {
            List<string> result = new List<string>();
            if (predsEx.Count == 0 && predsNormal.Count == 0) {
                Console.WriteLine("No valid airport prediction!");
                return result;
            }

            result.Add("RAIM预测不可靠航段:");
            if(predsEx.Count == 0)
                result.Add("NONE");
            predsEx.ForEach(pred => {
                result.AddRange(pred.Content);
                result.Add("");
            });
            result.Add("\r\n");
            result.Add("------------------------------------------------------------------");
            result.Add("\r\n");

            //if (predsNormal.Count != 0) {
            //    int maxContentSize = isAirport ? 5 : 6;
            //    string prefixName = isAirport ? "AIRPORT" : "ENROUT";
            //    var firstCompletePrediction = predsNormal.FirstOrDefault(pred => pred.Content.Count >= maxContentSize);
            //    if (firstCompletePrediction == null)
            //        firstCompletePrediction = predsNormal[0];

            //    result.Add(firstCompletePrediction.Content[0]);
            //    result.Add(firstCompletePrediction.Content[maxContentSize - 3]);
            //    result.Add(firstCompletePrediction.Content[maxContentSize - 2]);
            //    if (firstCompletePrediction.Content.Count >= maxContentSize)
            //        result.Add(firstCompletePrediction.Content[maxContentSize - 1]);

            //    predsNormal.ForEach(pred => {
            //        result.Add(string.Format("{0}:{1}", prefixName, pred.KeyVal));
            //    });
            //}

            if (predsNormal.Count != 0) {
                result.Add("RAIM预测可靠航段:");
                int maxContentSize = isAirport ? 5 : 6;
                string prefixName = isAirport ? "AIRPORT" : "ENROUT";
                var firstCompletePrediction = predsNormal.FirstOrDefault(pred => pred.Content.Count >= maxContentSize);
                if (firstCompletePrediction == null)
                    firstCompletePrediction = predsNormal[0];

                result.Add(firstCompletePrediction.Content[0]);
                result.Add(firstCompletePrediction.Content[maxContentSize - 3]);

                var finalKeys = predsNormal.Select(pred => pred.RNPList).Aggregate((a, b) => {
                    List<int> tmp = new List<int>();
                    tmp.AddRange(a);
                    tmp.AddRange(b);
                    return tmp.Distinct().ToList();
                });

                finalKeys.OrderBy(key => key).ToList().ForEach(rnpKey => {
                    var targetPreds = predsNormal.Where(pred => pred.RNPList.Contains(rnpKey)).ToList();

                    if (targetPreds.Count == 0)
                        return;

                    string title = targetPreds[0].Content.FirstOrDefault(line => line.StartsWith(string.Format("RNP{0}.00", rnpKey)));
                    result.Add(title);
                    targetPreds.ForEach(pred => {
                        result.Add(string.Format("{0}:{1}", prefixName, pred.KeyVal));
                    });
                    result.Add("\r\n");
                });
            }
            return result;
        }

        public static void GenerateBriefView(bool isAirport, List<Prediction> predsEx, List<Prediction> predsNormal, string targetFileName) {
            Document document = new Document();
            Section section = document.AddSection();
            Paragraph paragraph = section.AddParagraph();
            paragraph.Format.Font = new Font("微软雅黑");
            paragraph.Format.Font.Color = Color.FromCmyk(100, 30, 20, 50);

            var result = GenerateBriefContents(isAirport, predsEx, predsNormal);
            result.ForEach(line => {
                paragraph.AddFormattedText(line + "\r\n", TextFormat.Bold);
            });

            document.UseCmykColor = true;
            const bool unicode = true;
            const PdfFontEmbedding embedding = PdfFontEmbedding.Always;
            PdfDocumentRenderer pdfRenderer = new PdfDocumentRenderer(unicode, embedding);
            pdfRenderer.Document = document;
            pdfRenderer.RenderDocument();
            pdfRenderer.PdfDocument.Save(targetFileName);
            Process.Start(targetFileName);
            Console.WriteLine(string.Format("{0}.pdf is generated!", targetFileName));
        }

        //static Document CreateDocument() {
        //    Document document = new Document();
        //    Section section = document.AddSection();
        //    Paragraph paragraph = section.AddParagraph();
        //    paragraph.Format.Font.Color = Color.FromCmyk(100, 30, 20, 50);
        //    paragraph.AddFormattedText("Hello, EA Manager!", TextFormat.Bold);
        //    return document;
        //}

        static Document CreateDocument(List<Prediction> preds) {
            Document document = new Document();
            Section section = document.AddSection();            
            Paragraph paragraph = section.AddParagraph();
            paragraph.Format.Font = new Font("微软雅黑");
            paragraph.Format.Font.Color = Color.FromCmyk(100, 30, 20, 50);
            preds.ForEach(pred => {
                pred.Content.ForEach(singleContent => {
                    paragraph.AddFormattedText(singleContent + "\r\n", TextFormat.Bold);
                });
                paragraph.AddFormattedText("\r\n", TextFormat.Bold);
            });
            return document;
        }
    }
}