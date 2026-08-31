using Project.Characters.Enemy.EnemyScripts.Combat;
using UnityEngine;

public class EnemyAttack 
{
    
}

public class HomingAttack : EntityAttack
{
    public void ExecuteAttack()
    {
        Debug.Log("Homing");
    }
}