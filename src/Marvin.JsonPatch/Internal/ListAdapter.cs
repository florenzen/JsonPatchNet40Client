﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;
using Marvin.JsonPatch.Properties;

namespace Marvin.JsonPatch.Internal
{
    public class ListAdapter : IAdapter
    {
        public bool TryAdd(
            object target,
            string segment,
            IContractResolver contractResolver,
            object value,
            out string errorMessage)
        {
            var list = (IList)target;

            if (!TryGetListTypeArgument(list, out var typeArgument, out errorMessage))
            {
                return false;
            }

            if (!TryGetPositionInfo(list, segment, OperationType.Add, out var positionInfo, out errorMessage))
            {
                return false;
            }

            if (!TryConvertValue(value, typeArgument, segment, out var convertedValue, out errorMessage))
            {
                return false;
            }

            if (positionInfo.Type == PositionType.EndOfList)
            {
                list.Add(convertedValue);
            }
            else
            {
                list.Insert(positionInfo.Index, convertedValue);
            }

            errorMessage = null;
            return true;
        }

        public bool TryGet(
            object target,
            string segment,
            IContractResolver contractResolver,
            out object value,
            out string errorMessage)
        {
            var list = (IList)target;

            if (!TryGetListTypeArgument(list, out var typeArgument, out errorMessage))
            {
                value = null;
                return false;
            }

            if (!TryGetPositionInfo(list, segment, OperationType.Get, out var positionInfo, out errorMessage))
            {
                value = null;
                return false;
            }

            if (positionInfo.Type == PositionType.EndOfList)
            {
                value = list[list.Count - 1];
            }
            else
            {
                value = list[positionInfo.Index];
            }

            errorMessage = null;
            return true;
        }

        public bool TryRemove(
            object target,
            string segment,
            IContractResolver contractResolver,
            out string errorMessage)
        {
            var list = (IList)target;

            if (!TryGetListTypeArgument(list, out var typeArgument, out errorMessage))
            {
                return false;
            }

            if (!TryGetPositionInfo(list, segment, OperationType.Remove, out var positionInfo, out errorMessage))
            {
                return false;
            }

            if (positionInfo.Type == PositionType.EndOfList)
            {
                list.RemoveAt(list.Count - 1);
            }
            else
            {
                list.RemoveAt(positionInfo.Index);
            }

            errorMessage = null;
            return true;
        }

        public bool TryReplace(
            object target,
            string segment,
            IContractResolver contractResolver,
            object value,
            out string errorMessage)
        {
            var list = (IList)target;

            if (!TryGetListTypeArgument(list, out var typeArgument, out errorMessage))
            {
                return false;
            }

            if (!TryGetPositionInfo(list, segment, OperationType.Replace, out var positionInfo, out errorMessage))
            {
                return false;
            }

            if (!TryConvertValue(value, typeArgument, segment, out var convertedValue, out errorMessage))
            {
                return false;
            }

            if (positionInfo.Type == PositionType.EndOfList)
            {
                list[list.Count - 1] = convertedValue;
            }
            else
            {
                list[positionInfo.Index] = convertedValue;
            }

            errorMessage = null;
            return true;
        }

        public bool TryTraverse(
            object target,
            string segment,
            IContractResolver contractResolver,
            out object value,
            out string errorMessage)
        {
            var list = target as IList;
            if (list == null)
            {
                value = null;
                errorMessage = null;
                return false;
            }

            var index = -1;
            if (!int.TryParse(segment, out index))
            {
                value = null;
                errorMessage = Resources.FormatInvalidIndexValue(segment);
                return false;
            }

            if (index < 0 || index >= list.Count)
            {
                value = null;
                errorMessage = Resources.FormatIndexOutOfBounds(segment);
                return false;
            }

            value = list[index];
            errorMessage = null;
            return true;
        }

        private bool TryConvertValue(
            object originalValue,
            Type listTypeArgument,
            string segment,
            out object convertedValue,
            out string errorMessage)
        {
            var conversionResult = ConversionResultProvider.ConvertTo(originalValue, listTypeArgument);
            if (!conversionResult.CanBeConverted)
            {
                convertedValue = null;
                errorMessage = Resources.FormatInvalidValueForProperty(originalValue);
                return false;
            }

            convertedValue = conversionResult.ConvertedInstance;
            errorMessage = null;
            return true;
        }

        private bool TryGetListTypeArgument(IList list, out Type listTypeArgument, out string errorMessage)
        {
            // Arrays are not supported as they have fixed size and operations like Add, Insert do not make sense
            var listType = list.GetType();
            if (listType.IsArray)
            {
                errorMessage = Resources.FormatPatchNotSupportedForArrays(listType.FullName);
                listTypeArgument = null;
                return false;
            }
            else
            {
                var genericList = listType.GetType();
                if (genericList == null)
                {
                    errorMessage = Resources.FormatPatchNotSupportedForNonGenericLists(listType.FullName);
                    listTypeArgument = null;
                    return false;
                }
                else
                {
                    listTypeArgument = HeuristicallyDetermineType(list);
                    errorMessage = null;
                    return true;
                }
            }
        }

        private bool TryGetPositionInfo(
            IList list,
            string segment,
            OperationType operationType,
            out PositionInfo positionInfo,
            out string errorMessage)
        {
            if (segment == "-")
            {
                positionInfo = new PositionInfo(PositionType.EndOfList, -1);
                errorMessage = null;
                return true;
            }

            var position = -1;
            if (int.TryParse(segment, out position))
            {
                if (position >= 0 && position < list.Count)
                {
                    positionInfo = new PositionInfo(PositionType.Index, position);
                    errorMessage = null;
                    return true;
                }
                // As per JSON Patch spec, for Add operation the index value representing the number of elements is valid,
                // where as for other operations like Remove, Replace, Move and Copy the target index MUST exist.
                else if (position == list.Count && operationType == OperationType.Add)
                {
                    positionInfo = new PositionInfo(PositionType.EndOfList, -1);
                    errorMessage = null;
                    return true;
                }
                else
                {
                    positionInfo = new PositionInfo(PositionType.OutOfBounds, position);
                    errorMessage = Resources.FormatIndexOutOfBounds(segment);
                    return false;
                }
            }
            else
            {
                positionInfo = new PositionInfo(PositionType.Invalid, -1);
                errorMessage = Resources.FormatInvalidIndexValue(segment);
                return false;
            }
        }

        // Thanks to https://stackoverflow.com/questions/34211815/how-to-get-the-underlying-type-of-an-ilist-item
        private static Type HeuristicallyDetermineType(IList myList)
        {
            var enumerable_type =
                myList.GetType()
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericArguments().Length == 1)
                .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerable_type != null)
                return enumerable_type.GetGenericArguments()[0];

            if (myList.Count == 0)
                return null;

            return myList[0].GetType();
        }

        private struct PositionInfo
        {
            public PositionInfo(PositionType type, int index)
            {
                Type = type;
                Index = index;
            }

            public PositionType Type { get; }
            public int Index { get; }
        }

        private enum PositionType
        {
            Index, // valid index
            EndOfList, // '-'
            Invalid, // Ex: not an integer
            OutOfBounds
        }

        private enum OperationType
        {
            Add,
            Remove,
            Get,
            Replace
        }
    }
}
