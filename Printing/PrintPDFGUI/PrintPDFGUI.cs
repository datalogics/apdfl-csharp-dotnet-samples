using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Datalogics.PDFL;

/*
 * This sample printing a PDF file. It is similar to PrintPDF, but this
 * program provides a user interface.
 * 
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */

namespace PrintPDFGUI
{
    static class PrintPDFGUI
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PrintPDFForm());
        }
    }
}
