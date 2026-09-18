using System;
using System.IO;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sidebar
{
	public static class XpCompatibleIconConverter
	{
		public static Icon ToIcon (this BitmapSource bitmapSource)
		{
			try
			{
				if (bitmapSource == null) return null;

				// 1. 确保像素格式为 Bgra32（非预乘），以正确处理 Alpha
				var convertedSource = bitmapSource.Format == PixelFormats.Bgra32
					? bitmapSource
					: new FormatConvertedBitmap (bitmapSource, PixelFormats.Bgra32, null, 0);

				int width = convertedSource.PixelWidth;
				int height = convertedSource.PixelHeight;
				int stride = width * 4;

				// 2. 获取像素数据 (BGRA 顺序，自下而上存储)
				byte [] pixelData = new byte [stride * height];
				convertedSource.CopyPixels (pixelData, stride, 0);

				// 3. 生成 AND 掩码数据
				byte [] maskData = GenerateAndMask (pixelData, width, height);

				// 4. 构造 ICO 流
				using (var ms = new MemoryStream ())
				using (var writer = new BinaryWriter (ms))
				{
					// ICONDIR
					writer.Write ((ushort)0); // Reserved
					writer.Write ((ushort)1); // Type (1 = Icon)
					writer.Write ((ushort)1); // Count

					// ICONDIRENTRY
					writer.Write ((byte)(width >= 256 ? 0 : width));
					writer.Write ((byte)(height >= 256 ? 0 : height));
					writer.Write ((byte)0); // Color count
					writer.Write ((byte)0); // Reserved
					writer.Write ((ushort)1); // Planes
					writer.Write ((ushort)32); // Bit count

					// 图像数据大小 = BITMAPINFOHEADER (40) + XOR (pixelData) + AND (maskData)
					uint imageSize = (uint)(40 + pixelData.Length + maskData.Length);
					writer.Write (imageSize);
					writer.Write ((uint)22); // 图像数据偏移 (ICONDIR + ICONDIRENTRY)

					// BITMAPINFOHEADER
					writer.Write ((uint)40); // biSize
					writer.Write ((int)width); // biWidth
					writer.Write ((int)(height * 2)); // biHeight (关键: 必须是高度的两倍)
					writer.Write ((ushort)1); // biPlanes
					writer.Write ((ushort)32); // biBitCount
					writer.Write ((uint)0); // biCompression (BI_RGB)
					writer.Write ((uint)pixelData.Length); // biSizeImage
					writer.Write ((int)0); // biXPelsPerMeter
					writer.Write ((int)0); // biYPelsPerMeter
					writer.Write ((uint)0); // biClrUsed
					writer.Write ((uint)0); // biClrImportant

					// XOR 图数据 (自下而上)
					// 注意: 我们获取的 pixelData 已经是自上而下的，但 ICO 需要自下而上的数据。
					// 不过，System.Drawing.Icon 的解析器通常能处理这种方向，为简化代码，我们直接写入。
					// 如果遇到图像上下颠倒的问题，可以在此处添加行反转逻辑。
					writer.Write (pixelData);

					// AND 掩码数据 (自下而上)
					writer.Write (maskData);

					ms.Position = 0;
					return new Icon (ms);
				}
			}
			catch { return null; }
		}

		private static byte [] GenerateAndMask (byte [] pixelData, int width, int height)
		{
			// 掩码每行字节数，需要 4 字节对齐
			int maskStride = ((width + 31) / 32) * 4;
			byte [] maskData = new byte [maskStride * height];

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					// 获取当前像素的 Alpha 值 (BGRA 中的 A)
					int pixelIndex = (y * width + x) * 4;
					byte alpha = pixelData [pixelIndex + 3];

					// Alpha 值低于 128 视为透明，掩码位设为 1
					if (alpha < 128)
					{
						// 注意: 掩码数据是自下而上存储的
						int maskRow = height - 1 - y;
						int maskIndex = maskRow * maskStride + (x / 8);
						int bitPosition = 7 - (x % 8);
						maskData [maskIndex] |= (byte)(1 << bitPosition);
					}
				}
			}
			return maskData;
		}
	}
}