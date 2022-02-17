using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using zFramework.Pool;

namespace zFramework.Tests
{
    public class ObjectPoolTests
    {
        static readonly int count = 10;
        static GameObject prefab;

        public void Setup()
        {
            prefab = new GameObject("This is a prefab");
            prefab.CreatePool(count);
        }

        [Test, Order(0)]
        public void PooledObjectCount()
        {
            Setup();
            Assert.IsTrue(prefab.CountPooled() == 10);
        }

        [Test, Order(0)]
        public void SpawnedObjectCount()
        {
            Setup();
            var count = 5;
            for (int i = 0; i < count; i++)
            {
                prefab.Spawn();
            }
            Assert.IsTrue(prefab.CountSpawned() == count);
        }

        [Test(Author = "BianShanghai"), Order(1)]
        public void PoolSpawnTest()
        {
            Setup();
            for (int i = 0; i < count; i++)
            {
                prefab.Spawn();
                Assert.IsTrue(prefab.CountSpawned() == i + 1);
                Assert.IsTrue(prefab.CountPooled() == count - i - 1);
            }
        }

        [Test(Author = "BianShanghai"), Order(1)]
        public void PoolRecycleTest()
        {
            Setup();
            for (int i = 0; i < count; i++)
            {
                prefab.Spawn();
            }
            var spawned = prefab.GetSpawned();
            for (int i = 0; i < spawned.Count; i++)
            {
                spawned[i].Recycle();
            }
            Assert.IsTrue(prefab.CountSpawned() == 0);
            Assert.IsTrue(prefab.CountPooled() == count);
        }



        [Test, Order(2)]
        public void PoolSpawnOutsize()
        {
            var count = 10;
            var go = new GameObject("This is a prefab");
            go.CreatePool(count);
            for (int i = 0; i < count; i++)
            {
                go.Spawn();
            }
            // 当池中没有对象可取时的行为测试。
            // situation for requring a instance even the objectpool is empty.
            go.Spawn();
            Assert.IsTrue(go.CountPooled() == 0);
            Assert.IsTrue(go.CountSpawned() == count + 1);

            go.RecycleAll();
            Assert.IsTrue(go.CountPooled() == count + 1);
        }


        [Test, Order(2)]
        public void PoolRecycleAll()
        {
            int count = 10;
            var go = new GameObject("This is a prefab");
            go.CreatePool(count);
            Assert.IsTrue(go.CountPooled() == count);
            Assert.IsTrue(go.CountSpawned() == 0);

            go.Spawn();
            Assert.IsTrue(go.CountPooled() == count - 1);
            Assert.IsTrue(go.CountSpawned() == 1);

            go.RecycleAll();
            Assert.IsTrue(go.CountPooled() == count);
            Assert.IsTrue(go.CountSpawned() == 0);
        }
        [Test, Order(3)]
        public void PoolDestoryPooled()
        {
            var go = new GameObject("This is a prefab");
            go.CreatePool(10);
            go.Spawn(); //先从池中取出一个
            go.DestroyPooled();
            Assert.IsTrue(go.CountPooled() == 0);
            Assert.IsTrue(go.CountSpawned() == 1);
        }
        [Test, Order(3)]
        public void GetPooledObject()
        {
            var count = 10;
            var go = new GameObject("This is a prefab");
            go.CreatePool(count);
            go.Spawn();
            Assert.IsTrue(go.GetPooled().Count == count - 1);
        }

        [Test, Order(3)]
        public void GetSpawnedObject()
        {
            var count = 10;
            var go = new GameObject("This is a prefab");
            go.CreatePool(count);
            go.Spawn();
            Assert.IsTrue(go.GetSpawned().Count == 1);
        }

        [Test, Order(4)]
        public void PoolDestoryAll()
        {
            var go = new GameObject("This is a prefab");
            go.CreatePool(10);
            for (int i = 0; i < 10; i++)
            {
                go.Spawn();
            }
            go.DestroyAll();
            Assert.IsTrue(go.CountPooled() == 0);
            Assert.IsTrue(go.CountSpawned() == 0);
        }
        [Test, Order(5)]
        public void PoolRecycle()
        {
            var go = new GameObject("This is a prefab");
            go.CreatePool(10);
            var instance = go.Spawn();
            Assert.IsTrue(go.CountSpawned() == 1);
            instance.Recycle();
            Assert.IsTrue(go.CountSpawned() == 0);
        }
    }
}
