using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Katseülesanne_v2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string imagePath = "Image__2019-11-29__13-43-06.bmp";
            Bitmap image = new Bitmap(imagePath);

            // Algkolonn ehk pikslite kaugus, kust puulaua tuvastamist alustatakse
            int columnToStart = 1000;

            // Vahekohtade tuvastamise nihke suurus
            const int offsetDistance = 5;

            // Esimene samm on kaugus algkolonnist, kust otsitakse uuesti, kui puulauda ei tuvastata.
            // Teine samm on kaugus algkolonnist, kust otsitakse uuesti, kui leitakse mitu puulaudade vahelist eralduskohta
            const int stepColumnCorrection = 30;
            const int stepAnomalyCorrection = 20;
            bool foundColumn = false;

            // Lugerid, mille alusel määratakse mitu sammu tehakse ja lõpetavad programmi veateatega, kui ei leitud vastavat kolonni
            int counterNotFound = 1;
            int counterMultipleFound = 1;
            const int maxCount = 20;

            // Piirväärtused puulaudade vahekohtade leidmiseks ja puulaudade laiuse alguse ja lõpu määramiseks
            const int itensityTresholdVertical = 55;
            const int itensityTresholdHorizontal = 40;

            // Vahekoha leidmisel kasutatav hüpe (pikslite arv), puulaudade arv ja vahe asukoht
            const int ignorePixels = 15;
            int numObjs = 0;
            List<int> verticalEdgePos = new List<int>();

            // Mõõtepunktide hulk
            const int horizontalMeasuringPoints = 8;

            // Mitu mõõtepunkti kummagist ekstreemumist ei arvestata
            // 1 eemaldab suurima ja väikseima enne keskmise võtmist
            const int extremesToRemove = 1;

            // Programm otsib läbi ette määratud kolonni, et leida kahe puulaua eralduskoht pildilt.
            // Kui leiab mitu kohta, siis otsib uuesti liikudes ühele ja teisele poole vastavast kolonnist ette määratud sammu haaval
            do
            {
                // Kui kolonn ei kattu puulaudadega, siis toimub samasugune otsimine teise suurusega sammuga, et nad teineteist ei neutraliseeriks
                for (int row = 0; row < image.Height; row++)
                {
                    int intensity = getPixelIntensity(image, columnToStart, row);

                    // Juhul kui algselt määratud kolonn ei kattu puulauaga
                    if (intensity < itensityTresholdHorizontal && !foundColumn && counterNotFound < 15)
                    {
                        columnToStart = SearchNewColumn(counterNotFound, stepColumnCorrection, columnToStart);

                        counterNotFound++;
                    }
                    else
                    {
                        // Viga, kui puulauda ei tuvastata
                        if (counterNotFound == maxCount)
                        {
                            Console.WriteLine($"Error! Could not locate the object with {counterNotFound / 2} iterations of" +
                                $" searches with a step of {stepColumnCorrection} pixels! Recheck the image or starting column.");
                            Environment.Exit(1);
                        }

                        foundColumn = true;
                    }

                    // Laudade hulga ja nende eralduskohtade määramine. Et tuvastada riba, mitte oksakohti, siis kontrollin järgmise rida ning ka piksleid nihke kauguselt
                    if (intensity <= itensityTresholdVertical && (getPixelIntensity(image, columnToStart, row + 1) <= itensityTresholdVertical)
                        && (getPixelIntensity(image, columnToStart + offsetDistance, row + 1) <= itensityTresholdVertical) &&
                        (getPixelIntensity(image, columnToStart - offsetDistance, row + 1) <= itensityTresholdVertical))
                    {
                        verticalEdgePos.Add(row);
                        row += ignorePixels;
                    }
                }

                // Pildi kõrguse lisamine viimase laua alumiseks ääreks ning pildil olevate laudade hulga loendamine
                verticalEdgePos.Add(image.Height);
                numObjs = verticalEdgePos.Count;
                List<Tuple<int, double>> objWidths = new List<Tuple<int, double>>();

                // Leitud laudade segmenteerimine mõõtepunktideks ja vastavate punktide kõrguselt puulaua laiuste määramine
                for (int x = 1; x <= numObjs; x++)
                {
                    int objHeight = x == 1 ? verticalEdgePos[0] : verticalEdgePos[x - 1] - verticalEdgePos[x - 2];
                    int distanceBetweenPoints = objHeight / (horizontalMeasuringPoints + 1);
                    List<int> measuredWidths = new List<int>();

                    for (int z = 1; z <= horizontalMeasuringPoints; z++)
                    {
                        // Muutujad puulaua horiosontaalsete piirjoonte leidmiseks
                        bool leftFound = false;
                        bool rightFound = false;
                        int leftEdge = 0;
                        int rightEdge = 0;

                        for (int column = 1; column < image.Width; column++)
                        {
                            // 15 piksli kaugusel puulaudade vahekohast on esimene mõõtepunkt 
                            int row = verticalEdgePos[x - 1] - distanceBetweenPoints - 15;

                            // Horisontaalselt piksli valgustugevuse määramine lähenedes puulauale kummagilt poolt
                            int leftIntensity = 0;
                            int rightIntensity = 0;
                            if (!leftFound) { leftIntensity = getPixelIntensity(image, column, row); }
                            if (!rightFound) { rightIntensity = getPixelIntensity(image, image.Width - column, row); }

                            if (leftIntensity >= itensityTresholdHorizontal && !leftFound)
                            {
                                leftEdge = column;
                                leftFound = true;
                            }

                            if (rightIntensity >= itensityTresholdHorizontal && !rightFound)
                            {
                                rightEdge = image.Width - column;
                                rightFound = true;
                            }

                            // Kui mõlemad küljed on leitud, siis nende vahe salvestatakse massiivi
                            if (leftFound && rightFound)
                            {
                                measuredWidths.Add(rightEdge - leftEdge);
                                column = image.Width;
                            }

                        }
                    }

                    // Ekstreemväärtuste eemaldamine massiivist
                    measuredWidths.Sort();
                    for (int extreme = 0; extreme < extremesToRemove; extreme++)
                    {
                        measuredWidths.RemoveAt(0);
                        measuredWidths.RemoveAt(measuredWidths.Count - 1);
                    }

                    // Mõõtepunktide keskmise määramine ning millimeetritesse teisendamine
                    double width = measuredWidths.Average();
                    width = Math.Round(width * 0.1, 2);
                    objWidths.Add(Tuple.Create(x, width));
                }

                // Vastuse edastamine
                if (objWidths.Count == 2)
                {
                    Console.WriteLine("Objects are counted from top to bottom.");
                    foreach (var tuple in objWidths)
                    {
                        Console.WriteLine($"Width of object number {tuple.Item1} is {tuple.Item2}mm");
                    }
                }
                // Kui leitakse rohkem kui kaks puulauda pildil, siis otsitakse uuest kolonnist
                columnToStart = SearchNewColumn(counterMultipleFound, stepColumnCorrection, columnToStart);
                counterMultipleFound++;

                // Viga, kui vastavat kolonni ei tuvastata
                if (counterMultipleFound == maxCount)
                {
                    Console.WriteLine($"Error! Could not find the correct column with {counterMultipleFound / 2} iterations of" +
                        $" searches with a step of {stepAnomalyCorrection} pixels! Try lowering searching step values and check the starting column." +
                        $"Check the image for anomalies and adjust brightness if needed. ");
                    Environment.Exit(1);
                }
            }
            while (numObjs > 2);
    }
    static int SearchNewColumn(int n, int step, int column)
        {
            // Puulaua otsimiseks uue kolonni valimine liikudes ühes ja teises suunas vastava sammu haaval
            int helperValue = 0;
            
            // Abiväärtus on 0, kui i on 1 või paarisarv. Ülejäänud paaritute arvude puhul 1
            helperValue = n % 2 == 0 ? 0 : 1;
            helperValue = n == 1 ? 0 : helperValue;

            // Uus kolonn valitakse n sammu kaugusel ja suund on -1 astmel n
            return column += step * (n - helperValue) * (int)Math.Pow(-1, n);
        }

    static int getPixelIntensity(Bitmap image, int column, int row)
        {
            // Ühtlase valgustugevuse leidmine vastavalt inimese nägemisele erinevate toonide alusel
            Color pixelColor = image.GetPixel(column, row);
            int intensity = (int)(0.299 * pixelColor.R + 0.587 * pixelColor.G + 0.114 * pixelColor.B);

            return intensity;
        }
}
}


