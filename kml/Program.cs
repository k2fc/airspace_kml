using System;
using SharpKml.Engine;
using SharpKml.Dom;
using SharpKml.Base;
using System.IO;
using System.Linq;
using System.Drawing;

namespace kml
{
    class Program
    {
        static void Main(string[] args)
        {
            Document document = new Document();
            Random r = new Random();
            int airspacenum = 0;
            
            foreach (var filename in args)
            {
                Console.WriteLine("Reading {0} as Airspace {1}", filename, airspacenum);
                Style style = new Style();
                style.Id = "airspace" + airspacenum;
                style.Line = new LineStyle();
                style.Line.Color = new Color32(128, 255, 255, 255);
                style.Polygon = new PolygonStyle();
                style.Polygon.Outline = true;
                style.Polygon.Fill = true;
                byte red = (byte)(r.Next(0, 256));
                byte grn = (byte)(r.Next(0, 256));
                byte blu = (byte)(r.Next(0, 256));
                style.Polygon.Color = new Color32(128, red, grn, blu);
                document.AddStyle(style);
                try
                {
                    FileStream fs = new FileStream(filename, FileMode.Open);
                    KmlFile kmlfile = KmlFile.Load(fs);
                    Kml kml = kmlfile.Root as Kml;
                    if (kml != null)
                    {
                        Folder folder = new Folder();
                        folder.Name = string.Format("Airspace {0}", airspacenum);
                        int areanum = 0;
                        foreach (var orig_placemark in kml.Flatten().OfType<Placemark>())
                        {
                            Placemark placemark = new Placemark();
                            if (orig_placemark.ExtendedData == null)
                                continue;
                            var data = orig_placemark.ExtendedData.SchemaData.First().SimpleData;
                            bool hastop = data.Count(x => x.Name == "UN_TOP") > 0;
                            bool hasbot = data.Count(x => x.Name == "UN_BOT") > 0;
                            bool hasname = data.Count(x => x.Name == "AREA_ID") > 0;
                            if (hasname)
                                placemark.Name = data.Where(x => x.Name == "AREA_ID").First().Text;
                            else
                                placemark.Name = string.Format("Airspace {0} Area {1}", airspacenum, areanum);
                            Console.WriteLine("Working on {0}.", placemark.Name);
                            if (!hastop)
                            {
                                Console.WriteLine("No top defined. Skipping");
                                continue;
                            }
                            string topstring = data.Where(x => x.Name == "UN_TOP").First().Text;
                            Console.WriteLine("Top altitude: {0}", topstring);
                            string botstring = "";
                            double bottom = 0;
                            if (hasbot)
                            {
                                botstring = data.Where(x => x.Name == "UN_BOT").First().Text;
                                bottom = double.Parse(botstring) * 0.3048;
                                Console.WriteLine("Bottom altitude: {0}", botstring);
                            }
                            double top = double.Parse(topstring) * 0.3048;
                            if (bottom > top)
                            {
                                Console.WriteLine("Swapping top and bottom altitudes");
                                var newbottom = top;
                                top = bottom;
                                bottom = newbottom;

                                var newbs = topstring;
                                topstring = botstring;
                                botstring = newbs;
                            }
                            top += 152.4;
                            bottom -= 152.4;
                            if (top == 152.4)
                            {
                                Console.WriteLine("Top altitude is 0. Skipping."); 
                                placemark.Geometry = null;
                                continue;
                            }
                            MultipleGeometry geometry = orig_placemark.Geometry as MultipleGeometry;
                            if (geometry == null)
                            {
                                Console.WriteLine("Geometry is null. Skipping.");
                                continue;
                            }
                            

                            Polygon polygon = geometry.Geometry.First() as Polygon;
                            LinearRing ring = polygon.OuterBoundary.LinearRing;
                            string altstring = bottom.ToString();
                            if (bottom <= 0)
                            {
                                Console.WriteLine("Bottom altitude is 0. Drawing top and extruding.");
                                polygon.Extrude = true;
                                polygon.AltitudeMode = AltitudeMode.Absolute;
                                foreach (var point in ring.Coordinates)
                                {
                                    point.Altitude = top;
                                }
                                placemark.Geometry = geometry;
                                altstring = "SFC";
                            }
                            else
                            {
                                Console.WriteLine("Bottom altitude is {0}. Drawing bottom and sides.", botstring);
                                altstring = botstring;
                                polygon.Extrude = false;
                                var sides = ring.Coordinates.Count - 1;
                                Polygon newPolygon = new Polygon();
                                polygon.AltitudeMode = AltitudeMode.Absolute;
                                newPolygon.OuterBoundary = new OuterBoundary();
                                newPolygon.AltitudeMode = AltitudeMode.Absolute;
                                LinearRing newRing = new LinearRing();
                                newPolygon.OuterBoundary.LinearRing = newRing;
                                var newcoords = new CoordinateCollection();
                                foreach (var point in ring.Coordinates)
                                {
                                    newcoords.Add(new Vector(point.Latitude, point.Longitude, top));
                                    point.Altitude = bottom;
                                }
                                newPolygon.OuterBoundary.LinearRing.Coordinates = newcoords;
                                geometry.AddGeometry(newPolygon);
                                placemark.Geometry = geometry;
                                var ring1 = ring.Coordinates.ToArray();
                                var ring2 = newRing.Coordinates.ToArray();
                                for (int i = 0; i < sides; i++)
                                {
                                    Polygon side = new Polygon();
                                    side.AltitudeMode = AltitudeMode.Absolute;
                                    side.OuterBoundary = new OuterBoundary();
                                    side.OuterBoundary.LinearRing = new LinearRing();
                                    side.OuterBoundary.LinearRing.Coordinates = new CoordinateCollection();
                                    int j = i + 1;
                                    if (j == sides)
                                        j = 0;
                                    Vector a, b, c, d;
                                    a = ring1[i];
                                    b = ring2[i];
                                    c = ring2[j];
                                    d = ring1[j];
                                    side.OuterBoundary.LinearRing.Coordinates.Add(a);
                                    side.OuterBoundary.LinearRing.Coordinates.Add(b);
                                    side.OuterBoundary.LinearRing.Coordinates.Add(c);
                                    side.OuterBoundary.LinearRing.Coordinates.Add(d);
                                    side.OuterBoundary.LinearRing.Coordinates.Add(a);
                                    geometry.AddGeometry(side);
                                }
                            }
                            if (topstring != botstring)
                                altstring += " - " + topstring;

                            placemark.Description = new Description();
                            placemark.Description.Text = altstring;

                            placemark.StyleUrl = new Uri("#airspace" + airspacenum, UriKind.Relative);

                            folder.AddFeature(placemark.Clone());
                        }
                        document.AddFeature(folder);
                        airspacenum++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            var outkml = new Kml();
            outkml.Feature = document;
            KmlFile outFile = KmlFile.Create(outkml, true);
            using (FileStream fs = new FileStream("output.kml", FileMode.Create))
                outFile.Save(fs);
            Console.WriteLine("Done! Press enter to end.");
            Console.ReadLine();
        }
    }
}
