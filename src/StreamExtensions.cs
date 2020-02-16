namespace ROS {
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    static class StreamExtensions {
        public static async Task<int> ReadBlockAsync(this Stream stream, byte[] destination, int offset, int count, CancellationToken cancellation) {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (destination is null) throw new ArgumentNullException(nameof(destination));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (offset + count > destination.Length) throw new IndexOutOfRangeException();

            int total = 0;
            while(count > 0) {
                int read = await stream.ReadAsync(destination, offset, count, cancellation).ConfigureAwait(false);
                if (read == 0) return total;
                total += read;
                offset += read;
                count -= read;
            }
            return total;
        }
    }
}
