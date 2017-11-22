﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis.Elfie.Model.Strings;
using Microsoft.CodeAnalysis.Elfie.Serialization;

using XForm.Data;

namespace XForm.IO
{
    public class TabularFileReader : IDataBatchEnumerator
    {
        private string _filePath;

        private ITabularReader _reader;
        private List<ColumnDetails> _columns;

        private String8Block _block;
        private String8[][] _cells;
        private int _currentBatchCount;

        public TabularFileReader(string filePath)
        {
            _filePath = filePath;
            Reset();

            _block = new String8Block();
            _cells = new String8[_columns.Count][];
        }

        public IReadOnlyList<ColumnDetails> Columns => _columns;

        public Func<DataBatch> ColumnGetter(int columnIndex)
        {
            //String8[] array = new String8[1];

            //return () =>
            //{
            //    array[0] = _reader.Current(columnIndex).ToString8();
            //    return DataBatch.All(array, 1);
            //};

            return () =>
            {
                return DataBatch.All(_cells[columnIndex], _currentBatchCount);
            };
        }

        public void Reset()
        {
            _reader = TabularFactory.BuildReader(_filePath);

            _columns = new List<ColumnDetails>();
            foreach (string columnName in _reader.Columns)
            {
                _columns.Add(new ColumnDetails(columnName, typeof(String8), false));
            }
        }

        public int Next(int desiredCount)
        {
            if (_cells[0] == null)
            {
                for (int i = 0; i < _cells.Length; ++i)
                {
                    _cells[i] = new String8[desiredCount];
                }
            }

            //return _reader.NextRow();

            _block.Clear();
            _currentBatchCount = 0;

            while (_reader.NextRow())
            {
                for (int i = 0; i < _cells.Length; ++i)
                {
                    _cells[i][_currentBatchCount] = _block.GetCopy(_reader.Current(i).ToString8());
                }

                _currentBatchCount++;
                if (_currentBatchCount == desiredCount) break;
            }

            return _currentBatchCount;
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
        }
    }
}
