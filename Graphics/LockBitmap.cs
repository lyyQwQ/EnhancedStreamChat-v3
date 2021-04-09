using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EnhancedStreamChat.Graphics
{
    public class LockBitmap
    {
        private readonly Bitmap source = null;
        private IntPtr Iptr = IntPtr.Zero;
        private BitmapData bitmapData = null;

        public byte[] Pixels { get; set; }
        public int Depth { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public LockBitmap(Bitmap source)
        {
            this.source = source;
        }

        /// <summary>
        /// Lock bitmap data
        /// </summary>
        public void LockBits()
        {
            try {
                // Get width and height of bitmap
                this.Width = this.source.Width;
                this.Height = this.source.Height;

                // get total locked pixels count
                var PixelCount = this.Width * this.Height;

                // Create rectangle to lock
                var rect = new Rectangle(0, 0, this.Width, this.Height);

                // get source bitmap pixel format size
                this.Depth = Image.GetPixelFormatSize(this.source.PixelFormat);

                // Check if bpp (Bits Per Pixel) is 8, 24, or 32
                if (this.Depth != 8 && this.Depth != 24 && this.Depth != 32) {
                    throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
                }

                // Lock bitmap and return bitmap data
                this.bitmapData = this.source.LockBits(rect, ImageLockMode.ReadWrite,
                                             this.source.PixelFormat);

                // create byte array to copy pixel values
                var step = this.Depth / 8;
                this.Pixels = new byte[PixelCount * step];
                this.Iptr = this.bitmapData.Scan0;

                // Copy data from pointer to array
                Marshal.Copy(this.Iptr, this.Pixels, 0, this.Pixels.Length);
            }
            catch (Exception ex) {
                throw ex;
            }
        }

        /// <summary>
        /// Unlock bitmap data
        /// </summary>
        public void UnlockBits()
        {
            try {
                // Copy data from byte array to pointer
                Marshal.Copy(this.Pixels, 0, this.Iptr, this.Pixels.Length);

                // Unlock bitmap data
                this.source.UnlockBits(this.bitmapData);
            }
            catch (Exception ex) {
                throw ex;
            }
        }

        /// <summary>
        /// Get the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Color GetPixel(int x, int y)
        {
            var clr = Color.Empty;

            // Get color components count
            var cCount = this.Depth / 8;

            // Get start index of the specified pixel
            var i = ((y * this.Width) + x) * cCount;

            if (i > this.Pixels.Length - cCount)
                throw new IndexOutOfRangeException();

            if (this.Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
            {
                var b = this.Pixels[i];
                var g = this.Pixels[i + 1];
                var r = this.Pixels[i + 2];
                var a = this.Pixels[i + 3]; // a
                clr = Color.FromArgb(a, r, g, b);
            }
            if (this.Depth == 24) // For 24 bpp get Red, Green and Blue
            {
                var b = this.Pixels[i];
                var g = this.Pixels[i + 1];
                var r = this.Pixels[i + 2];
                clr = Color.FromArgb(r, g, b);
            }
            if (this.Depth == 8)
            // For 8 bpp get color value (Red, Green and Blue values are the same)
            {
                var c = this.Pixels[i];
                clr = Color.FromArgb(c, c, c);
            }
            return clr;
        }

        /// <summary>
        /// Set the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="color"></param>
        public void SetPixel(int x, int y, Color color)
        {
            // Get color components count
            var cCount = this.Depth / 8;

            // Get start index of the specified pixel
            var i = ((y * this.Width) + x) * cCount;

            if (this.Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
            {
                this.Pixels[i] = color.B;
                this.Pixels[i + 1] = color.G;
                this.Pixels[i + 2] = color.R;
                this.Pixels[i + 3] = color.A;
            }
            if (this.Depth == 24) // For 24 bpp set Red, Green and Blue
            {
                this.Pixels[i] = color.B;
                this.Pixels[i + 1] = color.G;
                this.Pixels[i + 2] = color.R;
            }
            if (this.Depth == 8)
            // For 8 bpp set color value (Red, Green and Blue values are the same)
            {
                this.Pixels[i] = color.B;
            }
        }
    }
}
