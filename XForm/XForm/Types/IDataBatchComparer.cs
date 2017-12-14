﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using XForm.Data;
using XForm.Query;
using XForm.Transforms;

namespace XForm.Types
{
    public interface IDataBatchComparer
    {
        void WhereEquals(DataBatch left, DataBatch right, RowRemapper result);
        void WhereNotEquals(DataBatch left, DataBatch right, RowRemapper result);
        void WhereLessThan(DataBatch left, DataBatch right, RowRemapper result);
        void WhereLessThanOrEquals(DataBatch left, DataBatch right, RowRemapper result);
        void WhereGreaterThan(DataBatch left, DataBatch right, RowRemapper result);
        void WhereGreaterThanOrEquals(DataBatch left, DataBatch right, RowRemapper result);
    }

    public static class DataBatchComparerExtensions
    {
        public static Action<DataBatch, DataBatch, RowRemapper> TryBuild(this IDataBatchComparer comparer, CompareOperator cOp)
        {
            // Return the function for the desired comparison operation
            switch (cOp)
            {
                case CompareOperator.Equals:
                    return comparer.WhereEquals;
                case CompareOperator.NotEquals:
                    return comparer.WhereNotEquals;
                case CompareOperator.GreaterThan:
                    return comparer.WhereGreaterThan;
                case CompareOperator.GreaterThanOrEqual:
                    return comparer.WhereGreaterThanOrEquals;
                case CompareOperator.LessThan:
                    return comparer.WhereLessThan;
                case CompareOperator.LessThanOrEqual:
                    return comparer.WhereLessThanOrEquals;
                default:
                    throw new NotImplementedException(cOp.ToString());
            }
        }
    }
}
