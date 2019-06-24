using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#if !CLIENT
namespace QBitNinja
#else
namespace QBitNinja.Client
#endif
{
    internal class CompressionHandler : DelegatingHandler
    {
        public Collection<Compressor> Compressors { get; }

        public CompressionHandler()
        {
            Compressors = new Collection<Compressor>
            {
                new GZipCompressor(),
                new DeflateCompressor()
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (response.Content == null || request.Headers.AcceptEncoding == null)
            {
                return response;
            }

            // As per RFC2616.14.3:
            // Ignores encodings with quality == 0
            // If multiple content-codings are acceptable, then the acceptable content-coding with the highest non-zero qvalue is preferred.
            Compressor compressor = (from encoding in request.Headers.AcceptEncoding
                              let quality = encoding.Quality ?? 1.0
                              where quality > 0
                              join c in Compressors on encoding.Value.ToLowerInvariant() equals c.EncodingType.ToLowerInvariant()
                              orderby quality descending
                              select c).FirstOrDefault();

            if (compressor != null)
            {
                response.Content = new CompressedContent(response.Content, compressor);
            }

            return response;
        }
    }

    internal class DecompressionHandler : DelegatingHandler
    {
        public Collection<Compressor> Compressors;

        public DecompressionHandler(HttpMessageHandler inner) : base(inner)
        {
            Compressors = new Collection<Compressor>
            {
                new GZipCompressor(),
                new DeflateCompressor()
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string encoding = response.Content.Headers.ContentEncoding.FirstOrDefault(e => e != null);
            if (string.IsNullOrEmpty(encoding) || response.Content == null)
            {
                return response;
            }

            Compressor compressor = Compressors.FirstOrDefault(c => c.EncodingType.Equals(encoding, StringComparison.OrdinalIgnoreCase));
            if (compressor != null)
            {
                response.Content = await DecompressContentAsync(response.Content, compressor).ConfigureAwait(false);
            }

            return response;
        }

        private static async Task<HttpContent> DecompressContentAsync(
            HttpContent compressedContent,
            Compressor compressor)
        {
            using (compressedContent)
            {
                MemoryStream decompressed = new MemoryStream();
                Stream content = await compressedContent.ReadAsStreamAsync();
                await compressor.Decompress(content, decompressed).ConfigureAwait(false);
                decompressed.Position = 0; // set position back to 0 so it can be read again

                StreamContent newContent = new StreamContent(decompressed);

                // copy content type so we know how to load correct formatter
                newContent.Headers.ContentType = compressedContent.Headers.ContentType;
                return newContent;
            }
        }
    }

    internal class CompressedContent : HttpContent
    {
        private readonly MemoryStream _Buffer = new MemoryStream();

        public CompressedContent(HttpContent content, Compressor compressor)
        {
            using (content)
            {
                using (Stream compressionStream = compressor.CreateCompressionStream(_Buffer))
                {
                    content.CopyToAsync(compressionStream).GetAwaiter().GetResult();
                    foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
                    {
                        Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    Headers.ContentEncoding.Add(compressor.EncodingType);
                }

                Headers.ContentLength = _Buffer.Length;
                _Buffer.Position = 0;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _Buffer.Length;
            return true;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            using (_Buffer)
            {
                await _Buffer.CopyToAsync(stream);
            }
        }
    }
}