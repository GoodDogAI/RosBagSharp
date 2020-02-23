namespace ROS {
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public static class BagReaderExtensions {
        public static async ValueTask<RosBagHeader> ReadBagHeader(this RosBagReader reader, CancellationToken cancellation = default) {
            if (reader is null) throw new ArgumentNullException(nameof(reader));

            if (!await reader.ReadRecord(cancellation).ConfigureAwait(false))
                throw new RosBagException(RosBagReader.EndOfStream);
            if (reader.NodeType != RosBagNodeType.RecordHeader)
                throw new RosBagException(HeaderMissing);

            ulong firstRecordOffset = 0;
            uint connectionCount = 0;
            uint chunkCount = 0;
            var fields = RosBagHeader.Fields.None;
            while (fields != RosBagHeader.Fields.All) {
                if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                    throw new RosBagException(RosBagReader.EndOfStream);
                if (reader.NodeType != RosBagNodeType.HeaderFieldName)
                    throw new RosBagException(RequiredFieldMissing);

                switch (reader.Text) {
                case "index_pos":
                    if (fields.HasFlag(RosBagHeader.Fields.FirstRecordOffset))
                        throw new RosBagException(RosBagReader.DuplicateField);
                    firstRecordOffset = await reader.ReadUInt64(cancellation).ConfigureAwait(false);
                    fields |= RosBagHeader.Fields.FirstRecordOffset;
                    break;
                case "conn_count":
                    if (fields.HasFlag(RosBagHeader.Fields.ConnectionCount))
                        throw new RosBagException(RosBagReader.DuplicateField);
                    connectionCount = await reader.ReadUInt32(cancellation).ConfigureAwait(false);
                    fields |= RosBagHeader.Fields.ConnectionCount;
                    break;
                case "chunk_count":
                    if (fields.HasFlag(RosBagHeader.Fields.ChunkCount))
                        throw new RosBagException(RosBagReader.DuplicateField);
                    chunkCount = await reader.ReadUInt32(cancellation).ConfigureAwait(false);
                    fields |= RosBagHeader.Fields.ChunkCount;
                    break;
                case "op":
                    if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                        throw new RosBagException(RosBagReader.EndOfStream);
                    if (reader.CurrentRecordType != RosBagRecordType.BagHeader)
                        throw new RosBagException(HeaderMissing);
                    break;
                default:
                    Debug.WriteLine($"unknown bag header field: {reader.Text}");
                    if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                        throw new RosBagException(RosBagReader.EndOfStream);
                    break;
                }
            }

            // skip remaining fields
            await reader.Skip(cancellation).ConfigureAwait(false);

            // skip data
            if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                throw new RosBagException(RosBagReader.EndOfStream);

            return new RosBagHeader(
                firstRecordOffset: firstRecordOffset,
                connectionCount: connectionCount,
                chunkCount: chunkCount);
        }

        const string RequiredFieldMissing = "Required field is missing";
        const string HeaderMissing = "Bag header is missing";
    }
}
