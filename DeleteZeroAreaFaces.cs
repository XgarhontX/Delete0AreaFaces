#if UNITY_EDITOR
using System.Collections.Generic;
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
    public void DoDeleteBadFacesForProBuilderChildren()
    {
        var proBuilderMeshes = gameObject.GetComponentsInChildren<ProBuilderMesh>();
        Debug.Log("DeleteZeroAreaFaces DoDeleteBadFacesForProBuilderChildren child count: " + proBuilderMeshes.Length);

        foreach (var model in proBuilderMeshes)
        {
            if (1 << model.gameObject.layer == 1 << LayerMask.NameToLayer("Default"))
            {
                Debug.Log("DeleteZeroAreaFaces " + model.gameObject.name);
                DeleteProBuilderZeroAreaFaces(model);
            }
        }
    }

    public void DeleteColliderAndAddColliderToProBuilderMeshes()
    {
        foreach (Transform child in gameObject.transform)
        {
            //if found collider but not probuilder, del
            if (child.gameObject.TryGetComponent<MeshCollider>(out var mc) &&
                !child.gameObject.TryGetComponent<MeshRenderer>(out _))
            {
                DestroyImmediate(mc);
                child.gameObject.SetActive(false);
                continue;
            }

            //if found probuilder w/o collider, add
            if (child.gameObject.TryGetComponent<ProBuilderMesh>(out var pbm)
                && !child.gameObject.TryGetComponent<MeshCollider>(out _))
            {
                var mc1 = child.gameObject.AddComponent<MeshCollider>();
            }
        }
    }

    public int DeleteProBuilderZeroAreaFaces(ProBuilderMesh mesh, bool isDelete = true)
    {
        //weld all verts
        var verts = mesh.GetVertices();
        int[] indices = new int[verts.Length];
        Debug.Log("index count: " + indices.Length);
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        mesh.WeldVertices(indices, 0.025f);

        //setup DeleteProBuilderZeroAreaFaces
        verts = mesh.GetVertices();
        var faces = mesh.faces;

        List<Face> zeroAreafaces = new(50);

        //foreach face, find area and add to deletion list
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

                areaSum += area;
            }

            if (areaSum <= 0f) zeroAreafaces.Add(face);
        }

        //delete
        if (isDelete)
        {
            Debug.Log("DONE! Deleted: " + zeroAreafaces.Count);
            mesh.DeleteFaces(zeroAreafaces);
        }
        else
        {
            Debug.Log("DONE! Selected: " + zeroAreafaces.Count);
            mesh.SetSelectedFaces(zeroAreafaces);
        }

        //mesh update
        mesh.Optimize();
        EditorUtility.SetDirty(mesh.gameObject);
        mesh.Refresh();

        return zeroAreafaces.Count;
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
public class ProBuilderDelTrashFacesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        
        DeleteZeroAreaFaces myScript = (DeleteZeroAreaFaces) target;
        
        DrawDefaultInspector();
        
        EditorGUILayout.HelpBox("Steps:\n1. ProBuilderize the exported CSG Model's children.\n2. Click \"2.\" button\n3. If collider has outstanding issues, try \"3.\" button", MessageType.Info);
        
        foreach (Transform child in myScript.gameObject.transform)
        {
            foreach (Transform childsChild in child.transform)
            {
                if (childsChild.TryGetComponent<MeshRenderer>(out _) &&
                    !childsChild.TryGetComponent<ProBuilderMesh>(out _))
                {
                    EditorGUILayout.HelpBox("NOT PROBUILDERIZED!", MessageType.Error);
                    Debug.LogError(child.gameObject.name + ": Not Probuilderized");
                }
            }
        }
        
        if (GUILayout.Button("2. Do for children (wield & del 0 area face)"))
        {
            myScript.DoDeleteBadFacesForProBuilderChildren();
        }
        if (GUILayout.Button("3. Switch to collider to use ProBuilderMesh"))
        {
            myScript.DeleteColliderAndAddColliderToProBuilderMeshes();
        }
    }
}

#endif