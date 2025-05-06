using Datalogics.PDFL;
using System;

/*
 *
 * This sample program demonstrates the use of AddDigitalSignature.
 *
 * Copyright (c) 2025, Datalogics, Inc. All rights reserved.
 *
 */
namespace AddDigitalSignature
{
    class AddDigitalSignature
    {
        static void Main(string[] args)
        {
            Console.WriteLine("AddDigitalSignature Sample:");

            using (new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/SixPages.pdf";
                String sLogo = Library.ResourceDirectory + "Sample_Input/ducky_alpha.tif";
                String sOutput = "DigSig-out.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                if (args.Length > 1)
                    sOutput = args[1];

                if (args.Length > 2)
                    sLogo = args[2];

                Console.WriteLine("Input file: " + sInput);
                Console.WriteLine("Writing to output: " + sOutput);

                using (Document doc = new Document(sInput))
                {
                    using (Datalogics.PDFL.SignDoc sigDoc = new Datalogics.PDFL.SignDoc())
                    {
                        // Setup Sign params
                        sigDoc.FieldID = SignatureFieldID.CreateFieldWithQualifiedName;
                        sigDoc.FieldName = "Signature_es_:signatureblock";

                        // Set credential related attributes
                        sigDoc.DigestCategory = DigestCategory.Sha256;
                        sigDoc.CredentialDataFormat = CredentialDataFmt.NonPFX;
                        sigDoc.SetNonPfxSignerCert("Credentials/DER/RSA_certificate.der", 0, CredentialStorageFmt.OnDisk);
                        sigDoc.SetNonPfxPrivateKey("Credentials/DER/RSA_privKey.der", 0, CredentialStorageFmt.OnDisk);

                        // Setup the signer information
                        // (Logo image is optional)
                        sigDoc.SetSignerInfo(sOutput, 0.5, "John Doe", "Chicago, IL", "Approval", "Datalogics, Inc.",
                            DisplayTraits.KDisplayAll);

                        // Set the size and location of the signature box (optional)
                        // If not set, invisible signature will be placed on first page
                        sigDoc.SignatureBoxPageNumber = 0;
                        sigDoc.SignatureBoxRectangle = new Rect(100, 300, 400, 400);

                        // Setup Save params
                        sigDoc.OutputPath = sOutput;

                        // Finally, sign and save the document
                        sigDoc.AddDigitalSignature(doc);

                        Console.WriteLine();
                    }
                }
            }
        }
    }
}
