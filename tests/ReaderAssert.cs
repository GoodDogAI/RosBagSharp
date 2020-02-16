namespace ROS {
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    static class ReaderAssert {
        public static async Task HeaderFieldName(RosBagReader reader, string expectedName) {
            await reader.ReadAsync().ConfigureAwait(false);
            Assert.AreEqual(RosBagNodeType.HeaderFieldName, reader.NodeType);
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
    }
}
