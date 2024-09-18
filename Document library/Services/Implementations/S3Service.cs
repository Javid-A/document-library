using Amazon.S3;
using Amazon.S3.Model;
using Document_library.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Drawing;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;
using Document = Document_library.DAL.Entities.Document;
using WordRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WordBreak = DocumentFormat.OpenXml.Wordprocessing.Break;
//using SpreadSheetRun = DocumentFormat.OpenXml.Spreadsheet.Run;
//using SpreadSheetBreak = DocumentFormat.OpenXml.Spreadsheet.Break;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Document_library.Services.Implementations
{
    public class S3Service(IConfiguration configuration, IAmazonS3 amazonS3, DocumentDB context, UserManager<User> userManager) : IS3Service
    {
        readonly string _bucketName = "document-library-system";

        /// <summary>
        /// Retrieves the files associated with a user.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns>A service result containing the list of document DTOs.</returns>
        public async Task<ServiceResult<IEnumerable<DocumentDTO>>> GetFiles(string username)
        {
            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<IEnumerable<DocumentDTO>>.Failed("User not found");

            IEnumerable<Document> documents = context.Documents.Where(d => d.UserId == user.Id).AsEnumerable();

            IList<DocumentDTO> model = new List<DocumentDTO>();
            foreach (Document doc in documents)
            {
                DocumentDTO document = new DocumentDTO() { Name = doc.Name, Path = doc.Path, Type = doc.Type, Downloads = doc.Downloads, ThumbnailURL = SetThumbnailOfDocuments(doc.ThumbnailPath!), CreatedAt = doc.CreatedAt, UpdatedAt = doc.UpdatedAt };
                model.Add(document);
            }

            return ServiceResult<IEnumerable<DocumentDTO>>.Success(model);
        }
        /// <summary>
        /// Downloads a file from the S3 bucket.
        /// </summary>
        /// <param name="fileName">The name of the file to download.</param>
        /// <param name="username">The username of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentResponse}"/> with the downloaded file.</returns>
        public async Task<ServiceResult<DocumentResponse>> DownloadFileAsync(string fileName, string username)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return ServiceResult<DocumentResponse>.Failed("File name is required");
            }

            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<DocumentResponse>.Failed("User not found");

            GetObjectResponse s3Object;
            try
            {
                string keyWithFolder = $"{user.UserName}/{fileName.Trim()}";
                s3Object = await amazonS3.GetObjectAsync(_bucketName, keyWithFolder);

            }
            catch (AmazonS3Exception ex)
            {
                return ServiceResult<DocumentResponse>.Failed(ex.Message);
            }

            using var stream = s3Object.ResponseStream;

            //Create a memory stream to store the file content because the ResponseStream is not seekable
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            // Reset the stream position to the beginning
            memoryStream.Position = 0;

            // Get the content type (optional)
            string contentType = s3Object.Headers.ContentType;

            // Return the file as a downloadable file
            return ServiceResult<DocumentResponse>.Success(new DocumentResponse { Name = fileName, Stream = memoryStream, ContentType = contentType });
        }

        /// <summary>
        /// Downloads multiple files from the S3 bucket and creates a zip archive.
        /// </summary>
        /// <param name="fileNames">The names of the files to download.</param>
        /// <param name="username">The username of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentResponse}"/> with the zip archive.</returns>
        public async Task<ServiceResult<DocumentResponse>> DownloadFilesAsync(string[] fileNames, string username)
        {
            if (fileNames.Length == 0) return ServiceResult<DocumentResponse>.Failed("No files to download");

            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<DocumentResponse>.Failed("User not found");

            var zipStream = new MemoryStream();

            using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var fileName in fileNames)
                {
                    GetObjectResponse s3Object;
                    try
                    {
                        string keyWithFolder = $"{user.UserName}/{fileName.Trim()}";
                        s3Object = await amazonS3.GetObjectAsync(_bucketName, keyWithFolder);
                    }
                    catch (AmazonS3Exception ex)
                    {
                        return ServiceResult<DocumentResponse>.Failed(ex.Message);
                    }
                    var zipEntry = archive.CreateEntry(fileName);

                    using var entryStream = zipEntry.Open();
                    using var s3Stream = s3Object.ResponseStream;
                    await s3Stream.CopyToAsync(entryStream);
                }
            }

            zipStream.Position = 0;

            return ServiceResult<DocumentResponse>.Success(new DocumentResponse { Name = "files.zip", Stream = zipStream, ContentType = "application/zip" });
        }

        /// <summary>
        /// Shares a file by generating a shareable link with a JWT token.
        /// </summary>
        /// <param name="fileName">The name of the file to share.</param>
        /// <param name="username">The username of the user.</param>
        /// <param name="expirationInHours">The expiration time of the shareable link in hours.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{String}"/> with the shareable link.</returns>
        public async Task<ServiceResult<string>> ShareFile(string fileName, string username, int expirationInHours)
        {
            // Find the current user
            User? currentUser = await userManager.FindByNameAsync(username);

            if (currentUser == null) return ServiceResult<string>.Failed("User not found");

            // Fetch the document from the database to validate ownership
            var document = context.Documents.FirstOrDefault(d => d.Name == fileName.Trim() && d.UserId == currentUser.Id);

            // Ensure that the document exists and belongs to the current user
            if (document == null)
            {
                return ServiceResult<string>.Failed("Document not found");
            }

            // Create a JWT token that contains the file name and expiration time
            var token = GenerateShareToken(document.Path, expirationInHours);


            return ServiceResult<string>.Success(token).WithMessage("Link can be shared");
        }

        /// <summary>
        /// Uploads multiple files to the S3 bucket and saves the corresponding documents in the database.
        /// </summary>
        /// <param name="files">The collection of files to upload.</param>
        /// <param name="username">The username of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{IList{string}}"/> with the list of failed files, if any.</returns>
        public async Task<ServiceResult<IList<string>>> UploadFilesAsync(IFormFileCollection files, string username)
        {
            // Check if the file type is supported
            if (!IsValidDocumentType(files))
            {
                return ServiceResult<IList<string>>.Failed("File type not supported");
            }

            // Create a list to store the names of the files that failed to upload
            IList<string> failedFiles = [];
            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<IList<string>>.Failed("User not found");

            // Upload each file to the S3 bucket
            foreach (IFormFile file in files)
            {
                // Create a unique key for the file
                var keyWithFolder = $"{user.UserName}/{file.FileName}";
                using var fileStream = file.OpenReadStream();

                // Create a PutObjectRequest to upload the file
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = keyWithFolder,
                    InputStream = fileStream,
                    ContentType = file.ContentType
                };

                // Upload the file to the S3 bucket
                PutObjectResponse s3 = await amazonS3.PutObjectAsync(putRequest);
                if (s3.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    // Create a thumbnail key for the file
                    string keyForThumbnail = $"{user.UserName}/thumbnails/{Path.GetFileNameWithoutExtension(file.FileName)}.png";

                    // Process the file to generate a thumbnail image
                    byte[]? thumbnail = ProcessFile(file);

                    //If thumbnail is null, it means the file type is PDF
                    if (thumbnail != null)
                    {
                        using var memoryStream = new MemoryStream(thumbnail);
                        var uploadRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyForThumbnail,
                            InputStream = memoryStream,
                            ContentType = "image/png"
                        };

                        var response = await amazonS3.PutObjectAsync(uploadRequest);
                    }

                    Document? existedDocument = await context.Documents.FirstOrDefaultAsync(d => d.Path == keyWithFolder && d.UserId == user.Id);

                    //If the document does not exist, create a new document
                    if (existedDocument == null)
                    {
                        Document document = new() { Name = file.FileName, Path = keyWithFolder, Type = Path.GetExtension(file.FileName), ThumbnailPath = keyForThumbnail, User = user };
                        await context.Documents.AddAsync(document);
                    }
                    //If the document exists, update the UpdatedAt property
                    else
                    {
                        existedDocument.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    failedFiles.Add(file.FileName);
                }
            }
            //Save changes if there are any files that were uploaded successfully
            if (files.Count != failedFiles.Count)
            {
                await context.SaveChangesAsync();
            }

            //If there are any failed files, return a partial success result
            if (failedFiles.Any())
            {
                var failedFilesMessage = "Some files failed to upload: " + string.Join(", ", failedFiles);
                return ServiceResult<IList<string>>.Success(failedFiles).WithMessage(failedFilesMessage);
            }

            //If all files were uploaded successfully, return a success result
            return ServiceResult<IList<string>>.Success(failedFiles).WithMessage("All files uploaded successfully");
        }

        /// <summary>
        /// Downloads a file from the S3 bucket.
        /// </summary>
        /// <param name="token">The token used to authenticate the request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentResponse}"/> with the downloaded file.</returns>
        public async Task<ServiceResult<DocumentResponse>> DownloadSharedFile(string token)
        {
            if (token == null) return ServiceResult<DocumentResponse>.Failed("Token is required");

            ClaimsPrincipal principal = ValidateToken(token);

            Claim? fileNameClaim = principal.Claims.FirstOrDefault(c => c.Type == "fileName");

            if (fileNameClaim == null) return ServiceResult<DocumentResponse>.Failed("File name not found in token");

            string fileName = fileNameClaim.Value;

            Document? document = await context.Documents.FirstOrDefaultAsync(d => d.Path == fileName);
            if (document == null) return ServiceResult<DocumentResponse>.Failed("Document not found");

            GetObjectResponse s3Object;
            try
            {
                s3Object = await amazonS3.GetObjectAsync(_bucketName, document.Path);

            }
            catch (AmazonS3Exception ex)
            {

                return ServiceResult<DocumentResponse>.Failed(ex.Message);
            }
            using var stream = s3Object.ResponseStream;

            //Create a memory stream to store the file content because the ResponseStream is not seekable
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            // Reset the stream position to the beginning
            memoryStream.Position = 0;

            // Get the content type (optional)
            string contentType = s3Object.Headers.ContentType;

            document.Downloads++;

            await context.SaveChangesAsync();

            return ServiceResult<DocumentResponse>.Success(new DocumentResponse { Name = fileName, Stream = memoryStream, ContentType = contentType });

        }

        /// <summary>
        /// Retrieves information about a shared file based on the provided token.
        /// </summary>
        /// <param name="token">The token used to authenticate the request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentDTO}"/> with the information about the shared file.</returns>
        public async Task<ServiceResult<DocumentDTO>> GetSharedFile(string token)
        {
            if (token == null) return ServiceResult<DocumentDTO>.Failed("Token is required");
            ClaimsPrincipal principal;
            try
            {
                principal = ValidateToken(token);

            }
            catch (SecurityTokenExpiredException ex)
            {
                return ServiceResult<DocumentDTO>.Failed(ex.Message).WithStatusCode(StatusCodes.Status401Unauthorized).WithMessage("The token is expired");
            }

            Claim? fileNameClaim = principal.Claims.FirstOrDefault(c => c.Type == "fileName");

            if (fileNameClaim == null) return ServiceResult<DocumentDTO>.Failed("Token is invalid");

            string fileName = fileNameClaim.Value;

            Document? document = await context.Documents.FirstOrDefaultAsync(d => d.Path == fileName);
            if (document == null) return ServiceResult<DocumentDTO>.Failed("Document not found");

            //There is no need automatically map for this case
            DocumentDTO result = new() { Name = document.Name, Path = document.Path, Type = document.Type, Downloads = document.Downloads, ThumbnailURL = SetThumbnailOfDocuments(document.ThumbnailPath!), CreatedAt = document.CreatedAt, UpdatedAt = document.UpdatedAt };

            return ServiceResult<DocumentDTO>.Success(result);
        }

        /// <summary>
        /// Generates a JWT token for sharing a file with an expiration time.
        /// </summary>
        /// <param name="fileName">The name of the file to share.</param>
        /// <param name="expirationInHours">The expiration time of the shareable link in hours.</param>
        /// <returns>The generated JWT token as a string.</returns>
        string GenerateShareToken(string fileName, int expirationInHours)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("fileName", fileName),
            };

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationInHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        /// <summary>
        /// Validates a JWT token.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <returns>The <see cref="ClaimsPrincipal"/> representing the validated token.</returns>
        ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"]!,

                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"]!,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        /// <summary>
        /// Checks if the document types in the given collection of files are valid.
        /// </summary>
        /// <param name="files">The collection of files to check.</param>
        /// <returns>True if all document types are valid, false otherwise.</returns>
        static bool IsValidDocumentType(IFormFileCollection files)
        {
            List<string> allowedExtensions = [".pdf", ".docx", ".xlsx", ".txt", ".jpg", ".jpeg", ".png", ".bmp", ".gif"];
            foreach (IFormFile file in files)
            {
                string extension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Sets the thumbnail of the documents with the specified path.
        /// </summary>
        /// <param name="path">The path of the document.</param>
        /// <returns>The URL of the generated thumbnail.</returns>
        string? SetThumbnailOfDocuments(string path)
        {
            if (path == null) return null;
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = path,
                Expires = DateTime.UtcNow.AddSeconds(30)
            };

            return amazonS3.GetPreSignedURL(request);
        }
        /// <summary>
        /// Generates a thumbnail image based on the given text.
        /// </summary>
        /// <param name="text">The text to generate the thumbnail from.</param>
        /// <returns>The byte array of the generated thumbnail image.</returns>
        static byte[] GenerateThumbnailByText(string text)
        {
            int width = 450;
            int height = 600;
            if (text.Length > 2450)
            {
                text = text[..2450];
            }

            using Bitmap bitmap = new Bitmap(width, height);

            using Graphics graphics = Graphics.FromImage(bitmap);

            graphics.Clear(Color.White);

            Font font = new Font("Arial", 12, FontStyle.Regular, GraphicsUnit.Pixel);

            Brush brush = new SolidBrush(Color.Black);

            StringFormat stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near
            };

            graphics.DrawString(text, font, brush, new RectangleF(0, 0, width, height), stringFormat);

            // Save the bitmap to a memory stream
            using MemoryStream memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);
            return memoryStream.ToArray();
        }
        /// <summary>
        /// Resizes the given image stream to fit the thumbnail size.
        /// </summary>
        /// <param name="imageStream">The image stream to resize.</param>
        /// <returns>The byte array of the resized image.</returns>
        public byte[] ResizeImageForThumbnail(Stream imageStream)
        {
            using Image originalImage = Image.FromStream(imageStream);
            using Bitmap thumbnailBitmap = new Bitmap(450, 600);

            using (Graphics graphics = Graphics.FromImage(thumbnailBitmap))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                graphics.DrawImage(originalImage, 0, 0, 450, 600);
            }

            using MemoryStream thumbnailStream = new MemoryStream();
            thumbnailBitmap.Save(thumbnailStream, ImageFormat.Png);
            return thumbnailStream.ToArray();
        }
        /// <summary>
        /// Extracts the text content from a DOCX file.
        /// </summary>
        /// <param name="stream">The stream of the DOCX file.</param>
        /// <returns>The extracted text content.</returns>
        static string ExtractTextFromDocx(Stream stream)
        {
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
            StringBuilder text = new();

            var body = wordDoc.MainDocumentPart!.Document.Body;

            if (body != null)
            {
                foreach (var element in body.Descendants())
                {
                    if (element is WordRun run)
                    {
                        text.Append(run.InnerText);
                    }
                    else if (element is WordBreak || element is CarriageReturn)
                    {
                        text.Append(Environment.NewLine);
                    }
                    else if (element is TabChar)
                    {
                        text.Append('\t');
                    }
                    else if (element is Paragraph)
                    {
                        text.Append(Environment.NewLine);
                    }
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// Extracts the text content from an Excel file.
        /// </summary>
        /// <param name="stream">The stream of the Excel file.</param>
        /// <returns>The extracted text content.</returns>
        static string ExtractTextFromExcel(Stream stream)
        {
            StringBuilder extractedData = new();

            using (SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(stream, false))
            {
                WorkbookPart workbookPart = spreadsheetDocument.WorkbookPart!;

                Sheet firstSheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().First();

                WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!);

                SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

                int rowCount = 0;
                foreach (Row row in sheetData.Elements<Row>())
                {
                    // Limit to 30 rows for testing purposes
                    if (rowCount >= 30) break;

                    int cellCount = 0;

                    foreach (Cell cell in row.Elements<Cell>())
                    {
                        string columnLetter = GetColumnLetter(cell.CellReference!);

                        //This is for testing purposes only, to limit the columns to A-P
                        if (IsColumnInRange(columnLetter, "A", "P"))
                        {
                            // It needs more logic and time to extract the data from the cell for showing properly in the UI
                            // For now, just append the cell value and a pipe character
                            extractedData.Append(GetCellValue(spreadsheetDocument, cell)).Append('|');
                            cellCount++;
                        }

                        if (cellCount >= 20) break;
                    }
                    extractedData.Append(Environment.NewLine);
                    rowCount++;
                }
            }
            return extractedData.ToString();
        }
        /// <summary>
        /// Gets the column letter from the cell reference.
        /// </summary>
        /// <param name="cellReference">The cell reference.</param>
        /// <returns>The column letter.</returns>
        static string GetColumnLetter(string cellReference)
        {
            return new string(cellReference.Where(c => char.IsLetter(c)).ToArray());
        }
        /// <summary>
        /// Checks if the column letter is within the specified range.
        /// </summary>
        /// <param name="columnLetter">The column letter.</param>
        /// <param name="start">The start of the range.</param>
        /// <param name="end">The end of the range.</param>
        /// <returns>True if the column letter is within the range, false otherwise.</returns>
        static bool IsColumnInRange(string columnLetter, string start, string end)
        {
            return string.Compare(columnLetter, start, StringComparison.OrdinalIgnoreCase) >= 0 &&
                   string.Compare(columnLetter, end, StringComparison.OrdinalIgnoreCase) <= 0;
        }
        /// <summary>
        /// Gets the cell value from the specified cell in the spreadsheet document.
        /// </summary>
        /// <param name="document">The spreadsheet document.</param>
        /// <param name="cell">The cell.</param>
        /// <returns>The cell value.</returns>
        static string GetCellValue(SpreadsheetDocument document, Cell cell)
        {
            // Check if the cell value is stored as a shared string
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                // If the cell is a shared string, look up the value in the shared string table
                SharedStringTablePart stringTablePart = document.WorkbookPart.SharedStringTablePart;
                return stringTablePart.SharedStringTable.ElementAt(int.Parse(cell.CellValue.InnerText)).InnerText;
            }
            else
            {
                // Otherwise, return the cell value directly
                return cell.CellValue?.InnerText ?? string.Empty;
            }
        }
        /// <summary>
        /// Processes the given file and returns the thumbnail image.
        /// </summary>
        /// <param name="file">The file to process.</param>
        /// <returns>The byte array of the thumbnail image.</returns>
        public byte[]? ProcessFile(IFormFile file)
        {
            string extension = Path.GetExtension(file.FileName).ToLower();

            switch (extension)
            {
                case ".docx":
                    using (var stream = file.OpenReadStream())
                    {
                        string docxText = ExtractTextFromDocx(stream);
                        return GenerateThumbnailByText(docxText);
                    }

                case ".xlsx":
                    using (var stream = file.OpenReadStream())
                    {
                        string excelText = ExtractTextFromExcel(stream);
                        return GenerateThumbnailByText(excelText);
                    }

                case ".txt":
                    using (var streamReader = new StreamReader(file.OpenReadStream()))
                    {
                        string txtContent = streamReader.ReadToEnd();
                        return GenerateThumbnailByText(txtContent);
                    }

                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                    using (var stream = file.OpenReadStream())
                    {
                        return ResizeImageForThumbnail(stream);
                    }

                case ".pdf":
                    return null;

                default:
                    throw new NotSupportedException($"The file type {extension} is not supported.");
            }
        }

    }
}
