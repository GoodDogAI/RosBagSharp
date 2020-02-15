namespace ROS {
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class RosBagReader: IDisposable {
        readonly Stream source;
        readonly BinaryReader binary;
        readonly byte[] oneByte = new byte[1];
        StringBuilder? stringBuilder;
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

        public ValueTask<bool> ReadAsync(CancellationToken cancellation = default) => this.NodeType switch
        {
            RosBagNodeType.None => this.ReadFileHeader(cancellation),
            _ => throw new NotImplementedException(),
        };

        async ValueTask<bool> ReadFileHeader(CancellationToken cancellation) {
            this.stringBuilder ??= new StringBuilder(capacity: this.HeaderLengthLimit);
            this.stringBuilder.Length = 0;
            while(this.stringBuilder.Length < this.stringBuilder.Capacity) {
                int read = await this.source.ReadAsync(this.oneByte, 0, 1, cancellation).ConfigureAwait(false);
                if (read < 0) throw new RosBagException();
                if (this.oneByte[0] == '\n') {
                    this.NodeType = RosBagNodeType.FormatVersion;
                    return true;
                }
                this.stringBuilder.Append((char)this.oneByte[0]);
            }
            throw new RosBagException();
        }

        void Dispose() => this.binary.Dispose();
        void IDisposable.Dispose() => this.Dispose();
        public void Close() => this.Dispose();

        public RosBagReader(Stream source) {
            this.source = source ?? throw new System.ArgumentNullException(nameof(source));
            this.binary = new BinaryReader(source);
        }
    }
}
