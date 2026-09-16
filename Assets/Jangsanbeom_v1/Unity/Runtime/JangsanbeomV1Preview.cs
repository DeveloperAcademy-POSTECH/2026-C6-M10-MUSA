using UnityEngine;

public class JangsanbeomV1Preview : MonoBehaviour
{
    public Animator character;
    string activeClip="Idle";
    readonly string[] clips={"Idle","Call_Lure","Move","Ambush","Claw_Attack","Grab","Vanish","Hit","Stunned","Defeated"};
    float speed=1;
    void OnGUI()
    {
        if(character==null)return;
        GUILayout.BeginArea(new Rect(16,16,200,450),GUI.skin.box);
        GUILayout.Label("JANGSANBEOM v1");
        GUILayout.Label("Animation preview");
        foreach(var clip in clips)
            if(GUILayout.Button(clip.Replace('_',' '),GUILayout.Height(27))) Play(clip);
        GUILayout.Space(6);
        GUILayout.Label("Speed: "+speed.ToString("F2")+"x");
        speed=GUILayout.HorizontalSlider(speed,.2f,1.5f);character.speed=speed;
        if(GUILayout.Button("Replay"))Play(activeClip);
        GUILayout.EndArea();
    }
    void Play(string clip)
    {
        activeClip=clip;
        character.Play(clip,0,0);
        character.Update(0);
    }
}
