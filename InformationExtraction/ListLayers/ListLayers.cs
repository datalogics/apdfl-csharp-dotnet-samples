using System;
using System.Collections.Generic;
using Datalogics.PDFL;

/*
 * This sample searches for and lists the names of the color layers found in a PDF document.
 *  
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */

namespace ListLayers
{
    class ListLayers
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ListLayers Sample:");


            // ReSharper disable once UnusedVariable
            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/Layers.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                Console.WriteLine("Input file: " + sInput);

                Document doc = new Document(sInput);

                IList<OptionalContentGroup> ocgs = doc.OptionalContentGroups;
                foreach (OptionalContentGroup ocg in ocgs)
                {
                    Console.WriteLine(ocg.Name);
                    Console.Write("  Intent: [");
                    if (ocg.Intent.Count > 0)
                    {
                        IEnumerator<String> i = ocg.Intent.GetEnumerator();
                        i.MoveNext();
                        Console.Write(i.Current);
                        while (i.MoveNext())
                        {
                            Console.Write(", ");
                            Console.Write(i.Current);
                        }
                    }

                    Console.WriteLine("]");
                }

                OptionalContentContext ctx = doc.OptionalContentContext;
                Console.Write("Optional content states: [");
                IList<bool> states = ctx.GetOCGStates(ocgs);
                if (states.Count > 0)
                {
                    IEnumerator<bool> i = states.GetEnumerator();
                    i.MoveNext();
                    Console.Write(i.Current);
                    while (i.MoveNext())
                    {
                        Console.Write(", ");
                        Console.Write(i.Current);
                    }
                }

                Console.WriteLine("]");
            }
        }
    }
}
