namespace ROS {
    /// <summary>
    /// The bag header record occurs once in the file as the first record.
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct RosBagHeader {
        /// <summary>
        /// Offset of first record after the chunk section
        /// </summary>
        public long FirstRecordOffset { get; }
        /// <summary>
        /// Number of unique connections in the file
        /// </summary>
        public int ConnectionCount { get; }
        /// <summary>
        /// Number of chunk records in the file
        /// </summary>
        public int ChunkCount { get; }
    }
}
