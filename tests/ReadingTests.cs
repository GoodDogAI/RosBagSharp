namespace ROS {
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using NUnit.Framework;
    public class ReadingTests {
        [Test]
        public async Task IntegrationTest() {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(typeof(ReadingTests), name: "testdata.dataset.bag");
            using var reader = new RosBagReader(stream);

            Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
            Assert.AreEqual("#ROSBAG V2.0", reader.Text);

            uint expectedBytes = 69;
            Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
            Assert.AreEqual(RosBagNodeType.RecordHeader, reader.NodeType);
            Assert.AreEqual(expectedBytes, reader.RemainingEntryBytes);

            await ReaderAssert.HeaderFieldName(reader, "chunk_count").ConfigureAwait(false);
            await ReaderAssert.HeaderFieldValue<uint>(reader, 5, ToUInt32).ConfigureAwait(false);

            expectedBytes -= FieldSize("chunk_count", 4);

            await ReaderAssert.HeaderFieldName(reader, "conn_count").ConfigureAwait(false);
            await ReaderAssert.HeaderFieldValue<uint>(reader, 2, ToUInt32).ConfigureAwait(false);

            expectedBytes -= FieldSize("conn_count", 4);

            reader.Close();
        }

        static uint ToUInt32(byte[] bytes) => BitConverter.ToUInt32(bytes, 0);
        const uint HeaderFieldNameValueSeparatorSize = 1;
        const uint StringLengthSize = 4;
        static uint FieldSize(string name, uint byteCount) => checked(
            StringLengthSize + (uint)name.Length + HeaderFieldNameValueSeparatorSize + byteCount);
    }
}