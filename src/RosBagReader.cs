namespace ROS {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class RosBagReader: IDisposable {
        readonly Stream source;
        readonly byte[] readBuffer = new byte[8];
        readonly Stack<uint> entrySize = new Stack<uint>(capacity: 4);
        StringBuilder? stringBuilder;
        byte[]? bytesOutputBuffer;
        int headerLengthLimit;

        public virtual RosBagNodeType NodeType { get; private set; }
        public virtual string Text {
            get {
                if (this.stringBuilder is null)
                    throw new InvalidOperationException();
                // TODO: check node type
                return this.stringBuilder.ToString();
            }
        }
        // TODO: check node type
        public virtual ReadOnlySpan<byte> Bytes => this.bytesOutputBuffer ?? throw new InvalidOperationException();

        public virtual int HeaderLengthLimit {
            get => this.headerLengthLimit;
            set {
                if (value <= 8)
                    throw new ArgumentOutOfRangeException(nameof(this.HeaderLengthLimit));
                if (value != this.headerLengthLimit && this.stringBuilder != null)
                    this.stringBuilder.Capacity = value;
                this.headerLengthLimit = value;
            }
        }
        internal uint RemainingEntryBytes {
            get => this.entrySize.Peek();
            set {
                this.entrySize.Pop();
                this.entrySize.Push(value);
            }
        }

        public ValueTask<bool> ReadAsync(CancellationToken cancellation = default) => this.NodeType switch
        {
            RosBagNodeType.None => this.ReadFileHeader(cancellation),
            RosBagNodeType.FormatVersion => this.ReadRecord(cancellation),
            RosBagNodeType.RecordHeader when this.RemainingEntryBytes == 0 => this.ReadRecord(cancellation),
            RosBagNodeType.RecordHeader => this.ReadRecordHeaderFieldName(cancellation),
            RosBagNodeType.HeaderFieldName => this.ReadFieldValue(cancellation),
            RosBagNodeType.HeaderFieldValue when this.RemainingEntryBytes == 0 => this.ReadRecordData(cancellation),
            RosBagNodeType.HeaderFieldValue => this.ReadRecordHeaderFieldName(cancellation),
            _ => throw new NotImplementedException(),
        };

        async ValueTask<bool> ReadFileHeader(CancellationToken cancellation) {
            await this.ReadStringUntil('\n', cancellation).ConfigureAwait(false);
            this.NodeType = RosBagNodeType.FormatVersion;
            return true;
        }

        async ValueTask<StringBuilder> ReadStringUntil(char terminator, CancellationToken cancellation) {
            this.stringBuilder ??= new StringBuilder(capacity: this.HeaderLengthLimit);
            this.stringBuilder.Length = 0;
            while (this.stringBuilder.Length < this.stringBuilder.Capacity) {
                int read = await this.source.ReadAsync(this.readBuffer, 0, 1, cancellation).ConfigureAwait(false);
                if (read < 0) throw new RosBagException(EndOfStream);
                if (this.readBuffer[0] == terminator) {
                    return this.stringBuilder;
                }
                this.stringBuilder.Append((char)this.readBuffer[0]);
            }
            throw new RosBagException(HeaderTooLarge);
        }

        async ValueTask<bool> ReadRecord(CancellationToken cancellation) {
            int read = await this.source.ReadBlockAsync(this.readBuffer, 0, 4, cancellation).ConfigureAwait(false);
            if (read == 0) return false;
            if (read < 4) throw new RosBagException(EndOfStream);
            this.entrySize.Push(BitConverter.ToUInt32(this.readBuffer, 0));
            this.NodeType = RosBagNodeType.RecordHeader;
            return true;
        }

        async ValueTask<bool> ReadRecordHeaderFieldName(CancellationToken cancellation) {
            if (this.RemainingEntryBytes < 4) throw new RosBagException(NestedConstructIsTooLarge);
            int read = await this.source.ReadBlockAsync(this.readBuffer, 0, 4, cancellation).ConfigureAwait(false);
            if (read < 4) throw new RosBagException(EndOfStream);
            this.RemainingEntryBytes -= 4;
            uint fieldSize = BitConverter.ToUInt32(this.readBuffer, 0);
            if (fieldSize == 0) throw new RosBagException(BadConstructSize);
            if (fieldSize > this.RemainingEntryBytes) throw new RosBagException(NestedConstructIsTooLarge);
            this.RemainingEntryBytes -= fieldSize;
            this.entrySize.Push(fieldSize);
            return await this.ReadFieldName(cancellation).ConfigureAwait(false);
        }

        async ValueTask<bool> ReadFieldName(CancellationToken cancellation) {
            var result = await this.ReadStringUntil('=', cancellation).ConfigureAwait(false);
            if (result.Length + 1> this.RemainingEntryBytes)
                throw new RosBagException(NestedConstructIsTooLarge);
            this.RemainingEntryBytes -= checked((uint)(result.Length + 1));
            this.NodeType = RosBagNodeType.HeaderFieldName;
            return true;
        }

        async ValueTask<bool> ReadFieldValue(CancellationToken cancellation) {
            byte[] result = new byte[this.RemainingEntryBytes];
            this.bytesOutputBuffer = result;
            int read = await this.source.ReadBlockAsync(result, 0, checked((int)result.LongLength), cancellation).ConfigureAwait(false);
            if (read < result.Length) throw new RosBagException(EndOfStream);
            this.entrySize.Pop();
            this.NodeType = RosBagNodeType.HeaderFieldValue;
            return true;
        }

        async ValueTask<bool> ReadRecordData(CancellationToken cancellation) {
            throw new NotImplementedException();
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing)
                this.source.Dispose();
        }
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        public void Close() => this.Dispose();

        public RosBagReader(Stream source) {
            this.source = source ?? throw new System.ArgumentNullException(nameof(source));
        }

        const string HeaderTooLarge = "Header is larger, than the limit in " + nameof(HeaderLengthLimit);
        const string EndOfStream = "Unexpected end of stream";
        const string NestedConstructIsTooLarge = "Nested construct size is too large to fit into its parent";
        const string BadConstructSize = "Unexpected construct size";
    }
}
