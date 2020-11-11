using UnityEngine;
using System.Collections.Generic;

public sealed class ObjectPool : MonoBehaviour
{
    public enum StartupPoolMode { Awake, Start, CallManually };
    static ObjectPool _instance;
    public static ObjectPool Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = (ObjectPool)FindObjectOfType(typeof(ObjectPool));
                if (!_instance)
                {
                    var go = new GameObject("[ObjectPool]");
                    _instance = go.AddComponent<ObjectPool>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class StartupPool
    {
        public int size;
        public GameObject prefab;
    }

    static List<GameObject> tempList = new List<GameObject>();
    Dictionary<GameObject, List<GameObject>> pooledObjects = new Dictionary<GameObject, List<GameObject>>();
    Dictionary<GameObject, GameObject> spawnedObjects = new Dictionary<GameObject, GameObject>();
    public StartupPoolMode startupPoolMode;
    public StartupPool[] startupPools;

    bool startupPoolsCreated;
    void Awake()
    {
        if (_instance != null && _instance.gameObject != gameObject)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
        else
        {
            _instance = GetComponent<ObjectPool>();
            DontDestroyOnLoad(gameObject);
        }
        hideFlags = HideFlags.HideAndDontSave;
        if (startupPoolMode == StartupPoolMode.Awake)
            CreateStartupPools();
    }

    void Start()
    {
        if (startupPoolMode == StartupPoolMode.Start)
            CreateStartupPools();
    }

    public static void CreateStartupPools()
    {
        if (!Instance.startupPoolsCreated)
        {
            Instance.startupPoolsCreated = true;
            var pools = Instance.startupPools;
            if (pools != null && pools.Length > 0)
                for (int i = 0; i < pools.Length; ++i)
                    CreatePool(pools[i].prefab, pools[i].size);
        }
    }

    public static void CreatePool<T>(T prefab, int initialPoolSize) where T : Component => CreatePool(prefab.gameObject, initialPoolSize);
    public static void CreatePool(GameObject prefab, int initialPoolSize)
    {
        if (prefab != null && !Instance.pooledObjects.ContainsKey(prefab))
        {
            var list = new List<GameObject>();
            Instance.pooledObjects.Add(prefab, list);

            if (initialPoolSize > 0)
            {
                bool active = prefab.activeSelf;
                prefab.SetActive(false);
                Transform parent = Instance.transform;
                while (list.Count < initialPoolSize)
                {
                    var obj = Instantiate(prefab);
                    obj.transform.SetParent(parent);
                    list.Add(obj);
                }
                prefab.SetActive(active);
            }
        }
    }
    public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position) => Spawn(prefab, parent, position, Quaternion.identity);
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation) => Spawn(prefab, null, position, rotation);
    public static GameObject Spawn(GameObject prefab, Transform parent) => Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
    public static GameObject Spawn(GameObject prefab, Vector3 position) => Spawn(prefab, null, position, Quaternion.identity);
    public static GameObject Spawn(GameObject prefab) => Spawn(prefab, null, Vector3.zero, Quaternion.identity);
    public static T Spawn<T>(T prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component => Spawn(prefab.gameObject, parent, position, rotation).GetComponent<T>();
    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component => Spawn(prefab.gameObject, null, position, rotation).GetComponent<T>();
    public static T Spawn<T>(T prefab, Transform parent, Vector3 position) where T : Component => Spawn(prefab.gameObject, parent, position, Quaternion.identity).GetComponent<T>();
    public static T Spawn<T>(T prefab, Vector3 position) where T : Component => Spawn(prefab.gameObject, null, position, Quaternion.identity).GetComponent<T>();
    public static T Spawn<T>(T prefab, Transform parent) where T : Component => Spawn(prefab.gameObject, parent, Vector3.zero, Quaternion.identity).GetComponent<T>();
    public static T Spawn<T>(T prefab) where T : Component => Spawn(prefab.gameObject, null, Vector3.zero, Quaternion.identity).GetComponent<T>();
    public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        List<GameObject> list;
        Transform trans;
        GameObject obj;
        if (Instance.pooledObjects.TryGetValue(prefab, out list))
        {
            obj = null;
            if (list.Count > 0)
            {
                while (obj == null && list.Count > 0)
                {
                    obj = list[0];
                    list.RemoveAt(0);
                }
                if (obj != null)
                {
                    trans = obj.transform;
                    trans.SetParent(parent, !(parent is RectTransform));
                    trans.localPosition = position;
                    trans.localRotation = rotation;
                    obj.SetActive(true);
                    Instance.spawnedObjects.Add(obj, prefab);
                    return obj;
                }
            }
            obj = Instantiate(prefab);
            trans = obj.transform;
            trans.SetParent(parent, !(parent is RectTransform));
            trans.localPosition = position;
            trans.localRotation = rotation;
            obj.SetActive(true);
            Instance.spawnedObjects.Add(obj, prefab);
            return obj;
        }
        else
        {
            obj = Instantiate(prefab);
            trans = obj.GetComponent<Transform>();
            trans.SetParent(parent, !(parent is RectTransform));
            trans.localPosition = position;
            trans.localRotation = rotation;
            return obj;
        }
    }

    public static void Recycle<T>(T obj) where T : Component => Recycle(obj.gameObject);
    public static void Recycle(GameObject obj)
    {
        if (Instance.spawnedObjects.TryGetValue(obj, out GameObject prefab))
            Recycle(obj, prefab);
        else
            Destroy(obj);
    }
    static void Recycle(GameObject obj, GameObject prefab)
    {
        Instance.pooledObjects[prefab].Add(obj);
        Instance.spawnedObjects.Remove(obj);
        obj.transform.SetParent(Instance.transform,!(obj.transform is RectTransform));
        obj.SetActive(false);
    }

    public static void RecycleAll<T>(T prefab) where T : Component => RecycleAll(prefab.gameObject);
    public static void RecycleAll(GameObject prefab)
    {
        foreach (var item in Instance.spawnedObjects)
            if (item.Value == prefab)
                tempList.Add(item.Key);
        for (int i = 0; i < tempList.Count; ++i)
            Recycle(tempList[i]);
        tempList.Clear();
    }
    public static void RecycleAll()
    {
        tempList.AddRange(Instance.spawnedObjects.Keys);
        for (int i = 0; i < tempList.Count; ++i)
            Recycle(tempList[i]);
        tempList.Clear();
    }
    public static bool IsSpawned(GameObject obj) => Instance.spawnedObjects.ContainsKey(obj);
    public static int CountPooled<T>(T prefab) where T : Component => CountPooled(prefab.gameObject);
    public static int CountPooled(GameObject prefab)
    {
        if (Instance.pooledObjects.TryGetValue(prefab, out List<GameObject> list))
            return list.Count;
        return 0;
    }
    public static int CountSpawned<T>(T prefab) where T : Component => CountSpawned(prefab.gameObject);
    public static int CountSpawned(GameObject prefab)
    {
        int count = 0;
        foreach (var instancePrefab in Instance.spawnedObjects.Values)
            if (prefab == instancePrefab)
                ++count;
        return count;
    }

    public static int CountAllPooled()
    {
        int count = 0;
        foreach (var list in Instance.pooledObjects.Values)
            count += list.Count;
        return count;
    }

    public static List<GameObject> GetPooled(GameObject prefab, List<GameObject> list, bool appendList)
    {
        if (list == null)
            list = new List<GameObject>();
        if (!appendList)
            list.Clear();
        if (Instance.pooledObjects.TryGetValue(prefab, out List<GameObject> pooled))
            list.AddRange(pooled);
        return list;
    }
    public static List<T> GetPooled<T>(T prefab, List<T> list, bool appendList) where T : Component
    {
        if (list == null)
            list = new List<T>();
        if (!appendList)
            list.Clear();
        if (Instance.pooledObjects.TryGetValue(prefab.gameObject, out List<GameObject> pooled))
            for (int i = 0; i < pooled.Count; ++i)
                list.Add(pooled[i].GetComponent<T>());
        return list;
    }

    public static List<GameObject> GetSpawned(GameObject prefab, List<GameObject> list, bool appendList)
    {
        if (list == null)
            list = new List<GameObject>();
        if (!appendList)
            list.Clear();
        foreach (var item in Instance.spawnedObjects)
            if (item.Value == prefab)
                list.Add(item.Key);
        return list;
    }
    public static List<T> GetSpawned<T>(T prefab, List<T> list, bool appendList) where T : Component
    {
        if (list == null)
            list = new List<T>();
        if (!appendList)
            list.Clear();
        var prefabObj = prefab.gameObject;
        foreach (var item in Instance.spawnedObjects)
            if (item.Value == prefabObj)
                list.Add(item.Key.GetComponent<T>());
        return list;
    }

    public static void DestroyPooled<T>(T prefab) where T : Component => DestroyPooled(prefab.gameObject);
    public static void DestroyAll<T>(T prefab) where T : Component => DestroyAll(prefab.gameObject);

    public static void DestroyPooled(GameObject prefab)
    {
        if (Instance.pooledObjects.TryGetValue(prefab, out List<GameObject> pooled))
        {
            for (int i = 0; i < pooled.Count; ++i)
                Destroy(pooled[i]);
            pooled.Clear();
        }
    }
    public static void DestroyAll(GameObject prefab)
    {
        RecycleAll(prefab);
        DestroyPooled(prefab);
    }
}

public static class ObjectPoolExtensions
{
    public static void CreatePool<T>(this T prefab) where T : Component => ObjectPool.CreatePool(prefab, 0);
    public static void CreatePool<T>(this T prefab, int initialPoolSize) where T : Component => ObjectPool.CreatePool(prefab, initialPoolSize);
    public static void CreatePool(this GameObject prefab) => ObjectPool.CreatePool(prefab, 0);
    public static void CreatePool(this GameObject prefab, int initialPoolSize) => ObjectPool.CreatePool(prefab, initialPoolSize);
    public static T Spawn<T>(this T prefab, Transform parent, Vector3 position, Quaternion rotation) where T : Component => ObjectPool.Spawn(prefab, parent, position, rotation);
    public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation) where T : Component => ObjectPool.Spawn(prefab, null, position, rotation);
    public static T Spawn<T>(this T prefab, Transform parent, Vector3 position) where T : Component => ObjectPool.Spawn(prefab, parent, position, Quaternion.identity);
    public static T Spawn<T>(this T prefab, Vector3 position) where T : Component => ObjectPool.Spawn(prefab, null, position, Quaternion.identity);
    public static T Spawn<T>(this T prefab, Transform parent) where T : Component => ObjectPool.Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
    public static T Spawn<T>(this T prefab) where T : Component => ObjectPool.Spawn(prefab, null, Vector3.zero, Quaternion.identity);
    public static GameObject Spawn(this GameObject prefab, Transform parent, Vector3 position, Quaternion rotation) => ObjectPool.Spawn(prefab, parent, position, rotation);
    public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation) => ObjectPool.Spawn(prefab, null, position, rotation);
    public static GameObject Spawn(this GameObject prefab, Transform parent, Vector3 position) => ObjectPool.Spawn(prefab, parent, position, Quaternion.identity);
    public static GameObject Spawn(this GameObject prefab, Vector3 position) => ObjectPool.Spawn(prefab, null, position, Quaternion.identity);
    public static GameObject Spawn(this GameObject prefab, Transform parent) => ObjectPool.Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
    public static GameObject Spawn(this GameObject prefab) => ObjectPool.Spawn(prefab, null, Vector3.zero, Quaternion.identity);
    public static void Recycle<T>(this T obj) where T : Component => ObjectPool.Recycle(obj);
    public static void Recycle(this GameObject obj) => ObjectPool.Recycle(obj);
    public static void RecycleAll<T>(this T prefab) where T : Component => ObjectPool.RecycleAll(prefab);
    public static void RecycleAll(this GameObject prefab) => ObjectPool.RecycleAll(prefab);
    public static int CountPooled<T>(this T prefab) where T : Component => ObjectPool.CountPooled(prefab);
    public static int CountPooled(this GameObject prefab) => ObjectPool.CountPooled(prefab);
    public static int CountSpawned<T>(this T prefab) where T : Component => ObjectPool.CountSpawned(prefab);
    public static int CountSpawned(this GameObject prefab) => ObjectPool.CountSpawned(prefab);
    public static List<GameObject> GetSpawned(this GameObject prefab, List<GameObject> list, bool appendList) => ObjectPool.GetSpawned(prefab, list, appendList);
    public static List<GameObject> GetSpawned(this GameObject prefab, List<GameObject> list) => ObjectPool.GetSpawned(prefab, list, false);
    public static List<GameObject> GetSpawned(this GameObject prefab) => ObjectPool.GetSpawned(prefab, null, false);
    public static List<T> GetSpawned<T>(this T prefab, List<T> list, bool appendList) where T : Component => ObjectPool.GetSpawned(prefab, list, appendList);
    public static List<T> GetSpawned<T>(this T prefab, List<T> list) where T : Component => ObjectPool.GetSpawned(prefab, list, false);
    public static List<T> GetSpawned<T>(this T prefab) where T : Component => ObjectPool.GetSpawned(prefab, null, false);
    public static List<GameObject> GetPooled(this GameObject prefab, List<GameObject> list, bool appendList) => ObjectPool.GetPooled(prefab, list, appendList);
    public static List<GameObject> GetPooled(this GameObject prefab, List<GameObject> list) => ObjectPool.GetPooled(prefab, list, false);
    public static List<GameObject> GetPooled(this GameObject prefab) => ObjectPool.GetPooled(prefab, null, false);
    public static List<T> GetPooled<T>(this T prefab, List<T> list, bool appendList) where T : Component => ObjectPool.GetPooled(prefab, list, appendList);
    public static List<T> GetPooled<T>(this T prefab, List<T> list) where T : Component => ObjectPool.GetPooled(prefab, list, false);
    public static List<T> GetPooled<T>(this T prefab) where T : Component => ObjectPool.GetPooled(prefab, null, false);
    public static void DestroyPooled(this GameObject prefab) => ObjectPool.DestroyPooled(prefab);
    public static void DestroyPooled<T>(this T prefab) where T : Component => ObjectPool.DestroyPooled(prefab.gameObject);
    public static void DestroyAll(this GameObject prefab) => ObjectPool.DestroyAll(prefab);
    public static void DestroyAll<T>(this T prefab) where T : Component => ObjectPool.DestroyAll(prefab.gameObject);
}
