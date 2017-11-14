﻿using Microsoft.CodeAnalysis.Elfie.Model.Strings;
using System;
using System.IO;
using XForm.Data;

namespace XForm.IO
{
    public interface IColumnWriter : IDisposable
    {
        void Write(DataBatch batch);
    }

    public class String8ColumnWriter : IColumnWriter
    {
        private FileStream _bytesWriter;
        private FileStream _positionsWriter;

        private int[] _positionsBuffer;
        private byte[] _positionBytesBuffer;
        private int _position;

        public String8ColumnWriter(string tableRootPath, string columnName)
        {
            string columnFilePath = Path.Combine(tableRootPath, columnName);
            Directory.CreateDirectory(columnFilePath);

            _bytesWriter = new FileStream(Path.Combine(columnFilePath, "V.s.bin"), FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
            _positionsWriter = new FileStream(Path.Combine(columnFilePath, "Vp.i32.bin"), FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
        }

        public void Write(DataBatch batch)
        {
            Allocator.AllocateToSize(ref _positionsBuffer, batch.Count);
            Allocator.AllocateToSize(ref _positionBytesBuffer, batch.Count * 4);

            String8[] array = (String8[])batch.Array;
            for(int i = 0; i < batch.Count; ++i)
            {
                String8 value = array[batch.Index(i)];
                value.WriteTo(_bytesWriter);
                _position += value.Length;
                _positionsBuffer[i] = _position;
            }

            Buffer.BlockCopy(_positionsBuffer, 0, _positionBytesBuffer, 0, 4 * batch.Count);
            _positionsWriter.Write(_positionBytesBuffer, 0, 4 * batch.Count);
        }

        public void Dispose()
        {
            if(_bytesWriter != null)
            {
                _bytesWriter.Dispose();
                _bytesWriter = null;
            }

            if(_positionsWriter != null)
            {
                _positionsWriter.Dispose();
                _positionsWriter = null;
            }
        }
    }

    public class BinaryTableWriter : DataBatchEnumeratorWrapper
    {
        private string _tableRootPath;
        private Func<DataBatch>[] _getters;
        private IColumnWriter[] _writers;

        public BinaryTableWriter(IDataBatchEnumerator source, string tableRootPath) : base(source)
        {
            _tableRootPath = tableRootPath;
            Directory.CreateDirectory(tableRootPath);

            int columnCount = source.Columns.Count;

            _getters = new Func<DataBatch>[columnCount];
            _writers = new IColumnWriter[columnCount];
            for (int i = 0; i < columnCount; ++i)
            {
                _getters[i] = source.ColumnGetter(i);
                _writers[i] = new String8ColumnWriter(tableRootPath, source.Columns[i].Name);
            }

            SchemaSerializer.Write(_tableRootPath, _source.Columns);
        }

        public override int Next(int desiredCount)
        {
            int count = _source.Next(desiredCount);
            if (count == 0) return 0;

            for (int i = 0; i < _getters.Length; ++i)
            {
                _writers[i].Write(_getters[i]());
            }

            return count;
        }

        public override void Dispose()
        {
            base.Dispose();

            if(_writers != null)
            {
                foreach(IColumnWriter writer in _writers)
                {
                    writer.Dispose();
                }

                _writers = null;
            }
        }
    }
}