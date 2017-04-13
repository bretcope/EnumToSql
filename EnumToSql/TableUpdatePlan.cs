using System;
using System.Collections.Generic;

namespace EnumToSql
{
    class TableUpdatePlan
    {
        public List<Row> Add { get; }
        public List<Row> Update { get; }
        public List<Row> Delete { get; }
        public DeletionMode DeletionMode { get; }

        TableUpdatePlan(List<Row> add, List<Row> update, List<Row> delete, DeletionMode deletionMode)
        {
            Add = add;
            Update = update;
            Delete = delete;
            DeletionMode = deletionMode;
        }

        public static TableUpdatePlan Create(EnumInfo enumInfo, List<Row> existingRows, DeletionMode deletionMode)
        {
            var add = new List<Row>();
            var update = new List<Row>();
            var delete = new List<Row>();

            var values = enumInfo.Values;

            var vi = 0;
            var ri = 0;

            var valueCount = values.Length;
            var rowCount = existingRows.Count;

            while (true)
            {
                var value = vi < valueCount ? values[vi] : null;
                var row = ri < rowCount ? existingRows[ri] : null;

                if (value == null && row == null)
                    break;

                if (row == null || row.Id > value?.LongId)
                {
                    // The next row either doesn't exist, or it has an Id greater than the next value. This means a value exists with no matching row. So we
                    // need to create the row.
                    add.Add(value.GetRow());
                    vi++;
                }
                else if (value == null || value.LongId > row.Id)
                {
                    // The next value doesn't exist, or it has an Id greater than the next row. This means a row exists with no matching value. We should mark
                    // it for deletion, unless deletion mode is do nothing.
                    if (deletionMode == DeletionMode.Delete
                        || deletionMode == DeletionMode.TryDelete
                        || (deletionMode == DeletionMode.MarkAsInactive && row.IsActive))
                    {
                        delete.Add(row);
                    }

                    ri++;
                }
                else
                {
                    if (value.LongId != row.Id)
                        throw new Exception("TableUpdatePlan has broken logic. This is a bug.");

                    // We have a value and a matching row. Check if anything needs updated.
                    if (value.Name != row.Name || value.Description != row.Description || value.IsActive != row.IsActive)
                    {
                        update.Add(value.GetRow());
                    }

                    vi++;
                    ri++;
                }
            }

            return new TableUpdatePlan(add, update, delete, deletionMode);
        }
    }
}