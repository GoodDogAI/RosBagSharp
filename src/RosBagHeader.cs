namespace ROS {
    using System;

    /// <summary>
    /// The bag header record occurs once in the file as the first record.
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct RosBagHeader {
        /// <summary>
        /// Offset of first record after the chunk section
        /// </summary>
        public ulong FirstRecordOffset { get; }
        /// <summary>
        /// Number of unique connections in the file
        /// </summary>
        public uint ConnectionCount { get; }
        /// <summary>
        /// Number of chunk records in the file
        /// </summary>
        public uint ChunkCount { get; }

        /// <summary>Returns human-readable representation of this object</summary>
        public override string ToString() => $"connections: {this.ConnectionCount} chunks: {this.ChunkCount} offset: {this.FirstRecordOffset}";

        public RosBagHeader(ulong firstRecordOffset, uint connectionCount, uint chunkCount) {
            this.FirstRecordOffset = firstRecordOffset;
            this.ConnectionCount = connectionCount;
            this.ChunkCount = chunkCount;
        }

        [Flags]
        internal enum Fields: byte
        {
            None = 0,

            FirstRecordOffset = 0b0001,
            ConnectionCount = 0b0010,
            ChunkCount = 0b0100,

            All = FirstRecordOffset | ConnectionCount | ChunkCount,
        }
    }
}
