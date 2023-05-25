using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Datalogics.PDFL;

/*
 * This sample shows how to open a PDF document, search for text in the pages and highlight
 * the text.
 * 
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */

namespace DisplayPDF
{
    static class DisplayPDFApp
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            using (Library lib = new Library())
            {                    
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
                Application.Run(new DisplayPDFForm());                    
            }
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "Exception Occurred");
        }
    }
}
