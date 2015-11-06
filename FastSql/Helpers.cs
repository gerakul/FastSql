using System;

namespace Gerakul.FastSql
{
  internal static class Helpers
  {
    internal static void CheckFieldSelection(FieldsSelector fieldSelector, int sourceNum, int destinationNum, int commonNum)
    {
      switch (fieldSelector)
      {
        case FieldsSelector.Source:
          if (sourceNum > commonNum)
          {
            throw new InvalidOperationException("FieldsSelector has value FieldsSelector.Source, but source has fields which are not presented in destination");
          }
          break;
        case FieldsSelector.Destination:
          if (destinationNum > commonNum)
          {
            throw new InvalidOperationException("FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source");
          }
          break;
        case FieldsSelector.Common:
          break;
        case FieldsSelector.Both:
          if (sourceNum > commonNum || destinationNum > commonNum)
          {
            throw new InvalidOperationException("FieldsSelector has value FieldsSelector.Both, but source fields do not match with destination fields");
          }
          break;
        default:
          throw new ArgumentException(string.Format("Unknown FieldsSelector. Value: {0}", fieldSelector));
          break;
      }
    }
  }
}
