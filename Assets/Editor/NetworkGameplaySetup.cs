using TheShedding.Characters;
using TheShedding.Items;
using TheShedding.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>이슈 #42의 프리팹 구성을 재현하는 에디터 도구. 기존 씬은 저장하지 않는다.</summary>
public static class NetworkGameplaySetup
{
    [MenuItem("The Shedding/Configure Network Gameplay")]
    public static void Configure()
    {
        foreach (string path in new[] {
            "Assets/Prefabs/Characters/Dad.prefab", "Assets/Prefabs/Characters/Test_Mom.prefab",
            "Assets/Prefabs/Characters/Test_Baby.prefab", "Assets/Prefabs/Characters/Test_Dog.prefab",
            "Assets/Prefabs/Characters/Test_Robber.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<NetworkCharacterState>() == null) root.AddComponent<NetworkCharacterState>();
                if (root.GetComponent<CharacterInputNetworkAdapter>() == null) root.AddComponent<CharacterInputNetworkAdapter>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        const string placedPath = "Assets/Prefabs/Items/Placed_Trap.prefab";
        const string previewPath = "Assets/Prefabs/Items/Trap_Preview.prefab";
        CreateTrap(placedPath, true);
        CreateTrap(previewPath, false);
        var placed = AssetDatabase.LoadAssetAtPath<GameObject>(placedPath);
        var trap = AssetDatabase.LoadAssetAtPath<TrapItem>("Assets/Test_Item/Trap.asset");
        trap.placementPrefab = placed;
        trap.previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(previewPath);
        EditorUtility.SetDirty(trap);
        var prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (!prefabs.Contains(placed)) prefabs.Add(new NetworkPrefab { Prefab = placed });
        EditorUtility.SetDirty(prefabs);
        AssetDatabase.SaveAssets();
    }

    private static void CreateTrap(string path, bool networked)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            root.name = networked ? "Placed Trap" : "Trap Preview";
            root.transform.localScale = new Vector3(0.8f, 0.08f, 0.8f);
            // 설치물 표현용 마커. 함정 종류별 효과는 별도 게임플레이 시스템에서 처리한다.
            Object.DestroyImmediate(root.GetComponent<Collider>());
            if (networked) root.AddComponent<NetworkObject>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
