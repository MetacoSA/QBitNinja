using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

#if !CLIENT
namespace QBitNinja
#else
namespace QBitNinja.Client
#endif
{
	internal abstract class Compressor
	{
		public abstract string EncodingType
		{
			get;
		}
		public abstract Stream CreateCompressionStream(Stream output);
		public abstract Stream CreateDecompressionStream(Stream input);

		public virtual Task Compress(Stream source, Stream destination)
		{
			var compressed = CreateCompressionStream(destination);

			return Pump(source, compressed)
				.ContinueWith(task => compressed.Dispose());
		}

		public virtual Task Decompress(Stream source, Stream destination)
		{
			var decompressed = CreateDecompressionStream(source);

			return Pump(decompressed, destination)
				.ContinueWith(task => decompressed.Dispose());
		}

		protected virtual Task Pump(Stream input, Stream output)
		{
			return input.CopyToAsync(output);
		}
	}

	internal class DeflateCompressor : Compressor
	{
		private const string DeflateEncoding = "deflate";

		public override string EncodingType
		{
			get
			{
				return DeflateEncoding;
			}
		}

		public override Stream CreateCompressionStream(Stream output)
		{
			return new DeflateStream(output, CompressionMode.Compress, leaveOpen: true);
		}

		public override Stream CreateDecompressionStream(Stream input)
		{
			return new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
		}
	}

	internal class GZipCompressor : Compressor
	{
		private const string GZipEncoding = "gzip";

		public override string EncodingType
		{
			get
			{
				return GZipEncoding;
			}
		}

		public override Stream CreateCompressionStream(Stream output)
		{
			return new GZipStream(output, CompressionMode.Compress, leaveOpen: true);
		}

		public override Stream CreateDecompressionStream(Stream input)
		{
			return new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
		}
	}
}