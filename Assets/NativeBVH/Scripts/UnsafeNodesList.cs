using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NativeBVH {
    /// <summary>
    /// Holds and array of elements and a list of empty spots for elements.
    /// When an element is removed, its index is added to the list of empty spots, ready to be re-used.
    /// This avoids the shifting of element indices upon removal (and results in an array with holes in it).
    /// </summary>
    public unsafe struct UnsafeNodesList : IDisposable{
        [NativeDisableUnsafePtrRestriction]
        public void* ptr;
        public int length;
            
        [NativeDisableUnsafePtrRestriction]
        private UnsafeList* emptyIndices;
        
        private Allocator allocator;
        
        public static UnsafeNodesList* Create(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) {
            var handle = new AllocatorManager.AllocatorHandle { Value = (int)allocator };
            UnsafeNodesList* listData = AllocatorManager.Allocate<UnsafeNodesList>(handle);
            UnsafeUtility.MemClear(listData, UnsafeUtility.SizeOf<UnsafeNodesList>());

            listData->allocator = allocator;
            listData->emptyIndices = UnsafeList.Create(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), length, allocator);

            if (length != 0) {
                listData->Resize(length);
            }

            if (options == NativeArrayOptions.ClearMemory && listData->ptr != null) {
                UnsafeUtility.MemClear(listData->ptr, listData->length * UnsafeUtility.SizeOf<int>());
            }

            return listData;
        }

        public static void Destroy(UnsafeNodesList* listData) {
            var allocator = listData->allocator;
            listData->Dispose();
            AllocatorManager.Free(allocator, listData);
        }

        public void RemoveAt(int index) {
            emptyIndices->Add(index);
            UnsafeUtility.WriteArrayElement(ptr, index, default(Node));
        }

        public int Add(Node element) {
            if (emptyIndices->Length <= 0) {
                Resize(math.max(length * 2, 2));
            }

            var index = UnsafeUtility.ReadArrayElement<int>(emptyIndices->Ptr, emptyIndices->Length-1);
            emptyIndices->RemoveAt<int>(emptyIndices->Length - 1);
            
            UnsafeUtility.WriteArrayElement(ptr, index, element);
  
            return index;
        }
        
        public Node* Get(int index) {
            return (Node*) ((long) ptr + (long) index * sizeof (Node));
        }

        public void Dispose() {
            if (ptr != null) {
                AllocatorManager.Free(allocator, ptr);
                allocator = Allocator.Invalid; 
                ptr = null;
                length = 0;
                UnsafeList.Destroy(emptyIndices);
            }
        }
        
        private void Resize(int newLength) {
            void* newPointer = null;

            if (newLength > 0) {
                newPointer = AllocatorManager.Allocate(allocator, UnsafeUtility.SizeOf<Node>(),  UnsafeUtility.AlignOf<Node>(), newLength);

                if (length > 0) {
                    var itemsToCopy = math.min(length, newLength);
                    var bytesToCopy = itemsToCopy * UnsafeUtility.SizeOf<Node>();
                    UnsafeUtility.MemCpy(newPointer, ptr, bytesToCopy);
                }
            }

            AllocatorManager.Free(allocator, ptr);

            for (int i = newLength - 1; i >= length; i--) {
                emptyIndices->Add(i);
            }

            ptr = newPointer;
            length = newLength;
        }
        
        public Node* this[int index] => Get(index);
    }
}