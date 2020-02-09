namespace ROS {
    /// <summary>
    /// The bag header record occurs once in the file as the first record.
    /// </summary>
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
