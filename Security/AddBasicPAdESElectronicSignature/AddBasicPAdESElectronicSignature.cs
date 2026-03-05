using Datalogics.PDFL;
using System;

/*
 *
 * This sample program demonstrates the use of AddDigitalSignature for PAdES
 * (PDF Advanced Electronic Signatures) baseline signature type without a
 * signature policy. PAdES signatures conform to the ETSI standard and use
 * the ETSI.CAdES.detached SubFilter.
 *
 * Copyright (c) 2026, Datalogics, Inc. All rights reserved.
 *
 */
namespace AddBasicPAdESElectronicSignature
{
    class AddBasicPAdESElectronicSignature
    {
        static void Main(string[] args)
        {
            Console.WriteLine("AddBasicPAdESElectronicSignature Sample:");

            using (new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/SixPages.pdf";
                String sLogo = Library.ResourceDirectory + "Sample_Input/ducky_alpha.tif";
                String sOutput = "PAdESBaselineSignature-out.pdf";

                String sPEMCert = Library.ResourceDirectory + "Sample_Input/Credentials/PEM/ecSecP521r1Cert.pem";
                String sPEMKey = Library.ResourceDirectory + "Sample_Input/Credentials/PEM/ecSecP521r1Key.pem";

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
                        sigDoc.DigestCategory = DigestCategory.Sha384;
                        sigDoc.CredentialDataFormat = CredentialDataFmt.NonPFX;
                        sigDoc.SetNonPfxSignerCert(sPEMCert, 0, CredentialStorageFmt.OnDisk);
                        sigDoc.SetNonPfxPrivateKey(sPEMKey, 0, CredentialStorageFmt.OnDisk);

                        // Set the signature type to PAdES (PDF Advanced Electronic Signatures).
                        // This produces an ETSI.CAdES.detached signature conforming to the
                        // PAdES baseline profile without a signature policy.
                        sigDoc.DocSignType = SignatureType.PADES;

                        // Setup the signer information
                        // (Logo image is optional)
                        sigDoc.SetSignerInfo(sLogo, 0.5F, "John Doe", "Chicago, IL", "Approval", "Datalogics, Inc.",
                            DisplayTraits.KDisplayAll);

                        // Set the size and location of the signature box (optional)
                        // If not set, invisible signature will be placed on first page
                        sigDoc.SignatureBoxPageNumber = 0;
                        sigDoc.SignatureBoxRectangle = new Rect(100, 300, 400, 400);

                        // Setup Save params
                        sigDoc.OutputPath = sOutput;

                        // Finally, sign and save the document
                        sigDoc.AddDigitalSignature(doc);
                    }
                }
            }
        }
    }
}
