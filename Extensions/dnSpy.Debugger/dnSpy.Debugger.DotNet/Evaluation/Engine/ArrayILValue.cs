﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using dnSpy.Contracts.Debugger.DotNet.Evaluation;
using dnSpy.Debugger.DotNet.Interpreter;
using dnSpy.Debugger.DotNet.Metadata;

namespace dnSpy.Debugger.DotNet.Evaluation.Engine {
	struct ArrayObjectValue : IDisposable {
		public readonly DbgDotNetValue Value;
		readonly bool ownsValue;
		public ArrayObjectValue(DbgDotNetValue value) {
			Debug.Assert(!value.IsNullReference);
			if (value.IsReference) {
				Value = value.Dereference();
				ownsValue = true;
			}
			else {
				Value = value;
				ownsValue = false;
			}
			Debug.Assert(Value.IsArray);
		}

		public void Dispose() {
			if (ownsValue)
				Value.Dispose();
		}
	}

	sealed class ArrayILValue : TypeILValue, IDebuggerRuntimeILValue {
		public override DmdType Type => arrayValue.Type;
		DbgDotNetValue IDebuggerRuntimeILValue.GetDotNetValue() => arrayValue;

		readonly DebuggerRuntimeImpl runtime;
		readonly DbgDotNetValue arrayValue;
		readonly bool isSZArray;
		long cachedArrayLength;
		const long cachedArrayLength_uninitialized = -1;
		const long cachedArrayLength_error = -2;

		public ArrayILValue(DebuggerRuntimeImpl runtime, DbgDotNetValue arrayValue) {
			this.runtime = runtime;
			this.arrayValue = arrayValue;
			var type = arrayValue.Type.IsByRef ? arrayValue.Type.GetElementType() : arrayValue.Type;
			Debug.Assert(type.IsArray);
			isSZArray = type.IsSZArray;
			cachedArrayLength = cachedArrayLength_uninitialized;
		}

		public override bool Call(bool isCallvirt, DmdMethodBase method, ILValue[] arguments, out ILValue returnValue) {
			switch (method.SpecialMethodKind) {
			case DmdSpecialMethodKind.Array_Get:
				//TODO:
				break;

			case DmdSpecialMethodKind.Array_Set:
				//TODO:
				break;

			case DmdSpecialMethodKind.Array_Address:
				//TODO:
				break;
			}

			return base.Call(isCallvirt, method, arguments, out returnValue);
		}

		internal DbgDotNetValue ReadArrayElement(long index) {
			if ((ulong)index > uint.MaxValue)
				return null;
			using (var obj = new ArrayObjectValue(arrayValue)) {
				var elemValue = obj.Value.GetArrayElementAt((uint)index);
				if (elemValue != null)
					return runtime.RecordValue(elemValue);
				return null;
			}
		}

		internal void WriteArrayElement(uint index, object value) => runtime.SetArrayElementAt(arrayValue, index, value);

		public override ILValue LoadSZArrayElement(LoadValueType loadValueType, long index, DmdType elementType) {
			if (!isSZArray)
				return null;
			if ((ulong)index > uint.MaxValue)
				return null;
			using (var obj = new ArrayObjectValue(arrayValue)) {
				var elemValue = obj.Value.GetArrayElementAt((uint)index);
				if (elemValue != null)
					return runtime.CreateILValue(elemValue);
				return null;
			}
		}

		public override bool StoreSZArrayElement(LoadValueType loadValueType, long index, ILValue value, DmdType elementType) {
			if (!isSZArray)
				return false;
			if ((ulong)index > uint.MaxValue)
				return false;
			runtime.SetArrayElementAt(arrayValue, (uint)index, value);
			return true;
		}

		public override ILValue LoadSZArrayElementAddress(long index, DmdType elementType) {
			if (!isSZArray)
				return null;
			if ((ulong)index > uint.MaxValue)
				return null;
			return new ArrayElementAddress(runtime, this, (uint)index);
		}

		public override bool GetSZArrayLength(out long length) {
			if (!isSZArray)
				cachedArrayLength = cachedArrayLength_error;
			if (cachedArrayLength == cachedArrayLength_uninitialized) {
				using (var obj = new ArrayObjectValue(arrayValue)) {
					if (!obj.Value.Type.IsSZArray || !obj.Value.GetArrayCount(out var arrayCount))
						cachedArrayLength = cachedArrayLength_error;
					else
						cachedArrayLength = arrayCount;
				}
			}
			if (cachedArrayLength >= 0) {
				length = cachedArrayLength;
				return true;
			}
			Debug.Assert(cachedArrayLength == cachedArrayLength_error);
			length = 0;
			return false;
		}
	}
}