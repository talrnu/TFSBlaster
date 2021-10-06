using System.Collections.Generic;
using System.Text;

namespace TFSBlasterLib.Model.Connection
{
    internal class TfsRequestBody
    {
        public List<TfsRequestItem> Items = new List<TfsRequestItem>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var item in Items)
            {
                sb.AppendLine($"{item.op} {item.path}: {item.value}");
            }

            return sb.ToString();
        }
    }
}
