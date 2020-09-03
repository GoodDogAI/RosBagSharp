namespace ROS {
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    static class ReaderAssert {
        public static async Task RecordHeader(RosBagReader reader, uint? expectedBytes = null) {
            bool result = await reader.ReadAsync().ConfigureAwait(false);
            Assert.IsTrue(result, "Unexpected end of stream");
            Assert.AreEqual(RosBagNodeType.RecordHeader, reader.NodeType);
            if (expectedBytes != null)
                Assert.AreEqual(expectedBytes, reader.RemainingEntryBytes);
        }
        public static async Task HeaderFieldName(RosBagReader reader, string expectedName = null) {
            await reader.ReadAsync().ConfigureAwait(false);
            Assert.AreEqual(RosBagNodeType.HeaderFieldName, reader.NodeType);
            if (expectedName != null)
                Assert.AreEqual(expectedName, reader.Text);
        }

        public static async Task HeaderFieldValue(RosBagReader reader) {
            await reader.ReadAsync().ConfigureAwait(false);
            Assert.AreEqual(RosBagNodeType.HeaderFieldValue, reader.NodeType);
        }
        public static async Task HeaderFieldValue<T>(RosBagReader reader, T expectedValue, Func<byte[], T> decoder) {
            await HeaderFieldValue(reader).ConfigureAwait(false);
            var decoded = decoder(reader.Bytes.ToArray());
            Assert.AreEqual(expectedValue, decoded);
        }
        public static async Task HeaderFieldValue(RosBagReader reader, uint expectedValue) {
            uint actual = await reader.ReadUInt32().ConfigureAwait(false);
            Assert.AreEqual(expectedValue, actual);
        }
        public static async Task HeaderFieldValue(RosBagReader reader, ulong expectedValue) {
            ulong actual = await reader.ReadUInt64().ConfigureAwait(false);
            Assert.AreEqual(expectedValue, actual);
        }

        public static async Task<int> RecordData(RosBagReader reader, int? expectedBytes = null) {
            await Read(reader, RosBagNodeType.RecordDataHeader).ConfigureAwait(false);
            await Read(reader, RosBagNodeType.RecordData).ConfigureAwait(false);
            if (expectedBytes != null)
                Assert.AreEqual(expectedBytes, reader.Bytes.Length);
            return reader.Bytes.Length;
        }

        public static async Task Read(RosBagReader reader, RosBagNodeType nodeType) {
            bool read = await reader.ReadAsync().ConfigureAwait(false);
            Assert.IsTrue(read, "Unexpected end of stream");
            Assert.AreEqual(nodeType, reader.NodeType);
        }
    }
}
