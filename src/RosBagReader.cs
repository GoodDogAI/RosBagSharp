namespace ROS {
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class RosBagReader: IDisposable {
        readonly Stream source;

        public RosBagReader(Stream source) {
            this.source = source ?? throw new System.ArgumentNullException(nameof(source));
        }

        public async ValueTask<bool> ReadAsync() {
            throw new NotImplementedException();
        }

        public async ValueTask SkipAsync() {
            throw new NotImplementedException();
        }

        void Dispose() => this.source.Dispose();
        void IDisposable.Dispose() => this.Dispose();
        public void Close() => this.Dispose();
    }
}
