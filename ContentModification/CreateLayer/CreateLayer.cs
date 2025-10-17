using Datalogics.PDFL;

/*
 * This sample adds Optional Content Groups (layers) to a PDF document and
 * then adds Content to those layers.
 * 
 * The related ChangeLayerConfiguration program makes layers visible or invisible.
 * 
 * You can toggle back and forth to make a layer visible or invisible
 * in a PDF Viewer.
 *
 */
namespace CreateLayer
{
    class CreateLayer
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CreateLayer Sample:");

            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/ducky.pdf";
                String sOutput = "CreateLayer-out.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                if (args.Length > 1)
                    sOutput = args[1];

                Console.WriteLine("Input file: " + sInput + ", writing to " + sOutput);

                using (Document doc = new Document(sInput))
                {
                    Console.WriteLine("Opened a document.");

                    using (Page pg = doc.GetPage(0))
                    {
                        Image image = (Image)pg.Content.GetElement(0);
                        image.Matrix = new Matrix(image.Matrix.A * .5, 0, 0, image.Matrix.D * .5, image.Matrix.H, image.Matrix.V);

                        Image image2 = new Image(Library.ResourceDirectory + "Sample_Input/Image.png");

                        Text text = new Text();
                        Matrix matrix = new Matrix();
                        Font font = new Font("Helvetica");
                        GraphicState graphicState = new GraphicState();
                        TextState textState = new TextState();

                        matrix.A = 42;
                        matrix.D = 22;
                        matrix.H = 72;
                        matrix.V = 72;

                        TextRun textRun = new TextRun("sample text", font, graphicState, textState, matrix);
                        text.AddRun(textRun);

                        Text text2 = new Text();

                        matrix.A = 30;
                        matrix.D = 30;
                        matrix.H = 72;
                        matrix.V = 288;

                        TextRun textRun2 = new TextRun("Text definition provided here", font, graphicState, textState, matrix);
                        text2.AddRun(textRun2);

                        // Containers, Forms and Annotations can be attached to an
                        // OptionalContentGroup; other content (like Image) can
                        // be made optional by placing it inside a Container
                        Container imageContainer = new Container();
                        imageContainer.Content = new Content();
                        imageContainer.Content.AddElement(image);

                        Container imageContainer2 = new Container();
                        imageContainer2.Content = new Content();
                        imageContainer2.Content.AddElement(image2);

                        Container textContainer = new Container();
                        textContainer.Content = new Content();
                        textContainer.Content.AddElement(text);

                        Container textContainer2 = new Container();
                        textContainer2.Content = new Content();
                        textContainer2.Content.AddElement(text2);

                        using (Document newDoc = new Document())
                        {
                            using (Page newPage = newDoc.CreatePage(Document.BeforeFirstPage, pg.MediaBox))
                            {
                                newPage.Content.AddElement(imageContainer);
                                newPage.Content.AddElement(imageContainer2);
                                newPage.Content.AddElement(textContainer);
                                newPage.Content.AddElement(textContainer2);

                                // We create new OptionalContentGroups and place them in the OptionalContentConfig.Order array
                                List<OptionalContentGroup> ocgs = CreateNewOptionalContentGroups(newDoc, new List<string> { "Rubber Ducky", "PNG Logo", "Example Text", "Text Definition" });

                                AssociateOCGWithContainer(newDoc, ocgs[0], imageContainer);
                                AssociateOCGWithContainer(newDoc, ocgs[1], imageContainer2);
                                AssociateOCGWithContainer(newDoc, ocgs[2], textContainer);
                                AssociateOCGWithContainer(newDoc, ocgs[3], textContainer2);

                                newPage.UpdateContent();

                                newDoc.Save(SaveFlags.Full, sOutput);
                            }
                        }
                    }
                }
            }
        }

        public static List<OptionalContentGroup> CreateNewOptionalContentGroups(Document doc, List<string> names)
        {
            List<OptionalContentGroup> ocgs = new List<OptionalContentGroup>();

            OptionalContentGroup ocg = new OptionalContentGroup(doc, names[0]);
            OptionalContentGroup ocg2 = new OptionalContentGroup(doc, names[1]);
            OptionalContentGroup ocg3 = new OptionalContentGroup(doc, names[2]);
            OptionalContentGroup ocg4 = new OptionalContentGroup(doc, names[3]);

            ocgs.Add(ocg);
            ocgs.Add(ocg2);
            ocgs.Add(ocg3);
            ocgs.Add(ocg4);

            // Add it to the Order array -- this is required so that it will appear in the 'Layers' panel in a PDF Viewer.
            OptionalContentOrderArray order_list = doc.DefaultOptionalContentConfig.Order;

            OptionalContentOrderArray grouping = new OptionalContentOrderArray(doc, "Image Grouping");
            grouping.Add(new OptionalContentOrderLeaf(ocg));
            grouping.Add(new OptionalContentOrderLeaf(ocg2));

            OptionalContentOrderArray grouping2 = new OptionalContentOrderArray(doc, "Text Grouping");
            grouping2.Add(new OptionalContentOrderLeaf(ocg3));
            grouping2.Add(new OptionalContentOrderLeaf(ocg4));

            order_list.Insert(order_list.Length, grouping);
            order_list.Insert(order_list.Length, grouping2);

            return ocgs;
        }

        // Associate a Container with an OptionalContentGroup via an OptionalContentMembershipDict.
        // This function associates a Container with a single OptionalContentGroup and uses
        // a VisibilityPolicy of AnyOn.
        public static void AssociateOCGWithContainer(Document doc, OptionalContentGroup ocg, Container cont)
        {
            // Create an OptionalContentMembershipDict.  The options here are appropriate for a
            // 'typical' usage; other options can be used to create an 'inverting' layer
            // (i.e. 'Display this content when the layer is turned OFF'), or to make the
            // Container's visibility depend on several OptionalContentGroups
            OptionalContentMembershipDict ocmd = new OptionalContentMembershipDict(doc, new OptionalContentGroup[] { ocg }, VisibilityPolicy.AnyOn);

            cont.OptionalContentMembershipDict = ocmd;
        }
    }
}
