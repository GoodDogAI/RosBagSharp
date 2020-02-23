namespace ROS {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class RosBagReader: IDisposable {
        readonly Stream source;
        readonly byte[] readBuffer = new byte[8];
        static readonly byte[] discardBuffer = new byte[1024];
        readonly Stack<uint> entrySize = new Stack<uint>(capacity: 4);
        StringBuilder? stringBuilder;
        byte[]? bytesOutputBuffer;
        int headerLengthLimit = 1024;

        public virtual RosBagNodeType NodeType { get; protected set; }
        public virtual RosBagRecordType CurrentRecordType { get; protected set; }
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
        public virtual uint RemainingEntryBytes {
            get => this.entrySize.Peek();
            protected set {
                this.entrySize.Pop();
                this.entrySize.Push(value);
            }
        }

        public ValueTask<bool> ReadAsync(CancellationToken cancellation = default) => this.NodeType switch
        {
            RosBagNodeType.None => this.ReadFileHeader(cancellation),
            RosBagNodeType.FormatVersion => this.ReadRecord(cancellation),
            RosBagNodeType.RecordHeader when this.RemainingEntryBytes == 0 => this.ReadRecordDataHeader(cancellation),
            RosBagNodeType.RecordHeader => this.ReadRecordHeaderFieldName(cancellation),
            RosBagNodeType.HeaderFieldName => this.ReadFieldValue(cancellation),
            RosBagNodeType.HeaderFieldValue when this.RemainingEntryBytes == 0
                => this.NextHeaderFieldNameOrContinueToData(cancellation),
            RosBagNodeType.HeaderFieldValue => this.ReadFieldValue(cancellation),
            RosBagNodeType.RecordDataHeader => this.ReadRecordData(cancellation),
            RosBagNodeType.RecordData when this.entrySize.Count == 0 || this.RemainingEntryBytes == 0 => this.ReadRecord(cancellation),
            RosBagNodeType.RecordData => this.ReadRecordData(cancellation),
            _ => throw new InvalidOperationException(),
        };
        public ValueTask<ulong> ReadUInt64(CancellationToken cancellation = default) {
            this.MustBeAbleToReadData(byteCount: 8);
            return this.UInt64(cancellation);
        }
        public ValueTask<uint> ReadUInt32(CancellationToken cancellation = default) {
            this.MustBeAbleToReadData(byteCount: 4);
            return this.UInt32(cancellation);
        }

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

        internal async ValueTask<bool> ReadRecord(CancellationToken cancellation) {
            int read = await this.source.ReadBlockAsync(this.readBuffer, 0, 4, cancellation).ConfigureAwait(false);
            if (read == 0) return false;
            if (read < 4) throw new RosBagException(EndOfStream);
            this.entrySize.Push(BitConverter.ToUInt32(this.readBuffer, 0));
            this.NodeType = RosBagNodeType.RecordHeader;
            this.CurrentRecordType = RosBagRecordType.Unknown;
            return true;
        }

        async ValueTask<bool> NextHeaderFieldNameOrContinueToData(CancellationToken cancellation) {
            if (this.RemainingEntryBytes != 0)
                throw new InvalidOperationException("Did not finish reading field value");
            this.PopSize();
            if (await this.ReadRecordHeaderFieldName(cancellation).ConfigureAwait(false))
                return true;
            this.PopSize();
            return await this.ReadRecordData(cancellation).ConfigureAwait(false);
        }

        void PopSize() {
            if (this.RemainingEntryBytes != 0)
                throw new InvalidOperationException();
            if (this.entrySize.Count == 1)
                throw new InvalidOperationException("Should never pop the last size entry");
            this.entrySize.Pop();
        }
        async ValueTask<bool> ReadRecordHeaderFieldName(CancellationToken cancellation) {
            if (this.RemainingEntryBytes == 0) return false;
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
            if (this.stringBuilder?.ToString() == "op") {
                if (result.Length != 1)
                    throw new RosBagException(BadConstructSize);
                if (this.CurrentRecordType != RosBagRecordType.Unknown)
                    throw new RosBagException(DuplicateField);
                this.CurrentRecordType = (RosBagRecordType)result[0];
            }
            this.NodeType = RosBagNodeType.HeaderFieldValue;
            this.RemainingEntryBytes = 0;
            return true;
        }

        async ValueTask<bool> ReadRecordDataHeader(CancellationToken cancellation) {
            if (this.RemainingEntryBytes != 0)
                throw new InvalidOperationException("Did not finish reading headers");
            this.RemainingEntryBytes = await this.UInt32(cancellation).ConfigureAwait(false);
            this.NodeType = RosBagNodeType.RecordDataHeader;
            return true;
        }

        async ValueTask<bool> ReadRecordData(CancellationToken cancellation) {
            byte[] result = new byte[this.RemainingEntryBytes];
            this.bytesOutputBuffer = result;
            int read = await this.source.ReadBlockAsync(result, 0, checked((int)result.LongLength), cancellation).ConfigureAwait(false);
            if (read < result.Length) throw new RosBagException(EndOfStream);
            this.RemainingEntryBytes = 0;
            this.entrySize.Pop();
            if (this.entrySize.Count != 0)
                throw new InvalidOperationException();
            this.NodeType = RosBagNodeType.RecordData;
            return true;
        }

        void MustBeAbleToReadData(uint byteCount) {
            if (this.RemainingEntryBytes < byteCount)
                throw new RosBagException(EndOfData);
            switch (this.NodeType) {
            case RosBagNodeType.HeaderFieldName:
                this.NodeType = RosBagNodeType.HeaderFieldValue;
                goto case RosBagNodeType.HeaderFieldValue;
            case RosBagNodeType.RecordDataHeader:
                this.NodeType = RosBagNodeType.RecordData;
                goto case RosBagNodeType.RecordData;
            case RosBagNodeType.HeaderFieldValue:
            case RosBagNodeType.RecordData:
                break;
            default:
                throw new InvalidOperationException();
            }
        }

        async ValueTask<ulong> UInt64(CancellationToken cancellation) {
            int read = await this.source.ReadBlockAsync(this.readBuffer, 0, 8, cancellation).ConfigureAwait(false);
            if (read < 8)
                throw new RosBagException(EndOfStream);
            this.RemainingEntryBytes -= (uint)read;
            return BitConverter.ToUInt64(this.readBuffer, 0);
        }
        async ValueTask<uint> UInt32(CancellationToken cancellation) {
            int read = await this.source.ReadBlockAsync(this.readBuffer, 0, 4, cancellation).ConfigureAwait(false);
            if (read < 4)
                throw new RosBagException(EndOfStream);
            this.RemainingEntryBytes -= (uint)read;
            return BitConverter.ToUInt32(this.readBuffer, 0);
        }

        public async ValueTask Skip(CancellationToken cancellation = default) {
            switch (this.NodeType) {
            case RosBagNodeType.HeaderFieldName:
            case RosBagNodeType.HeaderFieldValue:
                if (this.RemainingEntryBytes > 0) {
                    if (this.source.CanSeek) {
                        this.source.Seek(this.RemainingEntryBytes, SeekOrigin.Current);
                    } else {
                        await this.Discard(this.RemainingEntryBytes, cancellation).ConfigureAwait(false);
                    }
                }

                this.RemainingEntryBytes = 0;
                this.PopSize();
                this.NodeType = RosBagNodeType.RecordHeader;
                return;
            case RosBagNodeType.RecordHeader:
                await this.SkipRemainingHeaderFields(cancellation).ConfigureAwait(false);
                return;
            default:
                throw new NotSupportedException();
            }
        }

        async ValueTask Discard(uint byteCount, CancellationToken cancellation) {
            while (byteCount > 0) {
                int discardAtOnce = (int)Math.Min((uint)discardBuffer.Length, byteCount);
                int read = await this.source
                    .ReadBlockAsync(discardBuffer, 0, discardAtOnce, cancellation)
                    .ConfigureAwait(false);
                if (read < discardAtOnce)
                    throw new RosBagException(EndOfStream);
                byteCount -= (uint)read;
            }
        }

        async ValueTask SkipRemainingHeaderFields(CancellationToken cancellation) {
            if (this.NodeType != RosBagNodeType.RecordHeader)
                throw new InvalidOperationException();

            while (this.RemainingEntryBytes > 0) {
                if (!await this.ReadAsync(cancellation).ConfigureAwait(false))
                    throw new RosBagException(EndOfStream);

                await this.Skip(cancellation).ConfigureAwait(false);
            }
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
        internal const string EndOfStream = "Unexpected end of stream";
        const string EndOfData = "Unexpected end of data";
        const string NestedConstructIsTooLarge = "Nested construct size is too large to fit into its parent";
        const string BadConstructSize = "Unexpected construct size";
        internal const string DuplicateField = "Duplicate field";
    }
}
