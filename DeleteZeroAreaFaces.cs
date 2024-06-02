#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using EditorUtility = UnityEditor.EditorUtility;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class DeleteZeroAreaFaces : MonoBehaviour
{
    [Tooltip("aka Remove Doubles.\n0.1 gives false positives\n0.01 is sweet spot\n0.001 misses some")] public float WeldDistance = 0.01f;
    
    public void DoDeleteBadFacesForProBuilderChildren(bool isDelete = true)
    {
        //get children (includes self)
        var proBuilderMeshes = gameObject.GetComponentsInChildren<ProBuilderMesh>().ToList();
        Debug.Log("DeleteZeroAreaFaces DoDeleteBadFacesForProBuilderChildren child count: " + proBuilderMeshes.Count);

        //do
        foreach (var model in proBuilderMeshes)
        {
            if (1 << model.gameObject.layer == 1 << LayerMask.NameToLayer("Default"))
            {
                Debug.Log("DeleteZeroAreaFaces " + model.gameObject.name);
                DeleteProBuilderZeroAreaFaces(model, isDelete);
            }
        }
    }
    
    public void DoProBuilderizeForChildren()
    {
        //get children (includes self)
        var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>().ToList();
        Debug.Log("DeleteZeroAreaFaces DoProBuilderizeForChildren MeshFilter count: " + meshFilters.Count);

        //do
        for (var i = 0; i < meshFilters.Count; i++)
        {
            var meshFilter = meshFilters[i];
            EditorUtility.DisplayProgressBar("ProBuiderizing", i + " / " + meshFilters.Count, i / (1f * meshFilters.Count));

            if (1 << meshFilter.gameObject.layer == 1 << LayerMask.NameToLayer("Default"))
            {
                Debug.Log("DeleteZeroAreaFaces ProBuilderizing: " + meshFilter.gameObject.name);
                var meshImporter = new MeshImporter(meshFilter.gameObject);
                meshImporter.Import(new MeshImportSettings()
                {
                    quads = false,
                    smoothing = false,
                });

                var pbm = meshFilter.gameObject.GetComponent<ProBuilderMesh>();
                pbm.ToMesh();
                pbm.Refresh();

                // pbm.WeldVertices(pbm.faces.SelectMany(x => x.indexes), WeldDistance);
                // pbm.ToMesh();
                // pbm.Refresh();
                // pbm.unwrapParameters.hardAngle = 1; //TODO make optional?
                pbm.Optimize();
            }
        }
        
        EditorUtility.ClearProgressBar();
    }

    public void DeleteColliderAndAddColliderToProBuilderMeshes()
    {
        var cols = gameObject.GetComponentsInChildren<MeshCollider>();
        foreach (var col in cols)
        {
            var go = col.gameObject;
            if (!go.TryGetComponent<ProBuilderMesh>(out _)) go.SetActive(false);
        }
        
        var proBuilderMeshes = gameObject.GetComponentsInChildren<ProBuilderMesh>();
        foreach (var pbm in proBuilderMeshes)
        {
            if (!pbm.enabled) continue;
            var go = pbm.gameObject;
            if (!go.TryGetComponent<MeshCollider>(out _)) go.AddComponent<MeshCollider>();
        }
    }

    public int DeleteProBuilderZeroAreaFaces(ProBuilderMesh mesh, bool isDelete = true)
    {
        //weld all verts
        int[] indices = mesh.faces.SelectMany(x => x.indexes).ToArray();
        mesh.WeldVertices(indices, WeldDistance);
        
        //ProBuilder RemoveDegenerateTriangles()
        List<int> result = new List<int>();
        if (isDelete)
        {
            MeshValidation.RemoveDegenerateTriangles(mesh, result);
            Debug.Log("DONE! (ProBuilder) Deleted: " + result.Count);
        }
        
        //setup DeleteProBuilderZeroAreaFaces
        var verts = mesh.GetVertices();
        var faces = mesh.faces;
        
        //foreach face, find area and add to deletion list
        List<Face> zeroAreaFaces = new(128);
        foreach (var face in faces)
        {
            var areaSum = 0f;
            for (int i = 0; i < face.indexes.Count; i += 3)
            {
                if (face.indexes.Count % 3 != 0)
                {
                    Debug.LogError("face.indexes.Count % 3 != 0");
                    return 0;
                }
        
                var area = GetAreaOfFace(
                    verts[face.indexes[i]].position,
                    verts[face.indexes[i + 1]].position,
                    verts[face.indexes[i + 2]].position); 
        
                // areaSum += area;
                if (Mathf.Approximately(area, 0f) || area <= 0f)
                {
                    zeroAreaFaces.Add(face);
                    break;
                }
            }
        
            // if (areaSum <= 0f) zeroAreaFaces.Add(face);
        }
        
        //delete
        if (isDelete)
        {
            Debug.Log("DONE! (Dav) Deleted: " + zeroAreaFaces.Count);
            mesh.DeleteFaces(zeroAreaFaces);
        }
        else
        {
            Debug.Log("DONE! Selected: " + zeroAreaFaces.Count);
            mesh.SetSelectedFaces(zeroAreaFaces);
            ProBuilderEditor.selectMode = SelectMode.Face;
            ProBuilderEditor.Refresh();
        }
        
        // //weld all verts again
        // indices = mesh.faces.SelectMany(x => x.indexes).ToArray();
        // mesh.WeldVertices(indices, 0.1f);

        //mesh update
        MeshValidation.RemoveUnusedVertices(mesh);
        EditorUtility.SetDirty(mesh.gameObject);
        mesh.ToMesh();
        mesh.Refresh();
        mesh.Optimize();
        ProBuilderEditor.Refresh();
        
        return zeroAreaFaces.Count + result.Count;
    }

    public float GetAreaOfFace(Vector3 pt1, Vector3 pt2, Vector3 pt3)
    {
        // Debug.Log("GetAreaOfFace: " + pt1 + pt2 + pt3);
    
        float a = Vector3.Distance(pt1, pt2);
        // Debug.Log("a: " + a);
    
        float b = Vector3.Distance(pt2, pt3);
        // Debug.Log("b: " + b);
    
        float c = Vector3.Distance(pt3, pt1);
        // Debug.Log("c: " + c);
    
        float s = (a + b + c) / 2;
        // Debug.Log("s: " + s);
    
        float area = Mathf.Sqrt(s * (s - a) * (s - b) * (s - c));
        // Debug.Log("Area: " + area);
    
        return area;
    }
}

[CustomEditor(typeof(DeleteZeroAreaFaces))]
public class DeleteZeroAreaFacesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        
        DeleteZeroAreaFaces myScript = (DeleteZeroAreaFaces) target;
        
        DrawDefaultInspector();
        
        EditorGUILayout.HelpBox("Sometimes, meshes need manual select all verts and weld doubles.", MessageType.Info);
        
        if (GUILayout.Button("1. ProBuilderize")) myScript.DoProBuilderizeForChildren();
        if (GUILayout.Button("2. Delete0AreaFaces for self/children")) myScript.DoDeleteBadFacesForProBuilderChildren();
        if (GUILayout.Button("3. Switch children to collider to use ProBuilderMesh")) myScript.DeleteColliderAndAddColliderToProBuilderMeshes();
        
        GUILayout.Space(5);
        if (GUILayout.Button("2 (Debug). Select 0 area faces for self/children")) myScript.DoDeleteBadFacesForProBuilderChildren(false);
    }
}
#else
using UnityEngine;
public class DeleteZeroAreaFaces : MonoBehaviour
{
    private void Awake()
    {
        Destroy(this);
    }
}

#endif
