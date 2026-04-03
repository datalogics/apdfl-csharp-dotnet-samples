using Datalogics.PDFL;
using System;

/*
 *
 * This sample program demonstrates the use of AddDigitalSignature for PAdES
 * (PDF Advanced Electronic Signatures) with an explicit signature policy.
 * PAdES policy signatures (EPES) conform to the ETSI EN 319 142 standard,
 * use the ETSI.CAdES.detached SubFilter, and embed one or more signature
 * policy identifiers along with optional policy qualifiers.
 *
 * Copyright (c) 2026, Datalogics, Inc. All rights reserved.
 *
 */
namespace AddPAdESPolicySignature
{
    class AddPAdESPolicySignature
    {
        static void Main(string[] args)
        {
            Console.WriteLine("AddPAdESPolicySignature Sample:");

            using (new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/SixPages.pdf";
                String sLogo = Library.ResourceDirectory + "Sample_Input/ducky_alpha.tif";
                String sOutput = "PAdESPolicySignature-out.pdf";

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
                        // PAdES signatures use SHA-384 digest with EC credentials
                        sigDoc.DigestCategory = DigestCategory.Sha384;
                        sigDoc.CredentialDataFormat = CredentialDataFmt.NonPFX;
                        sigDoc.SetNonPfxSignerCert(sPEMCert, 0, CredentialStorageFmt.OnDisk);
                        sigDoc.SetNonPfxPrivateKey(sPEMKey, 0, CredentialStorageFmt.OnDisk);

                        // Set the signature type to PAdES (PDF Advanced Electronic Signatures).
                        // NOTE: Signature type must be set prior to adding policies.
                        sigDoc.DocSignType = SignatureType.PADES;

                        // Define a signature policy (SigPolicyId) using an OID.
                        sigDoc.AddSigPolicy("2.16.724.1.3.1.1.2.1.9");

                        // Add a policy qualifier (SPuri) pointing to the policy specification document.
                        sigDoc.AddSigPolicyQualifierURI(
                            "https://sede.administracion.gob.es/politica_de_firma_anexo_1.pdf");

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
