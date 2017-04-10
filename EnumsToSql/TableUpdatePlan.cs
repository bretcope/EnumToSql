using System.Collections.Generic;

namespace EnumsToSql
{
    class TableUpdatePlan
    {
        public List<Row> Add { get; set; }
        public List<Row> Delete { get; set; }
        public List<Row> Update { get; set; }
    }
}