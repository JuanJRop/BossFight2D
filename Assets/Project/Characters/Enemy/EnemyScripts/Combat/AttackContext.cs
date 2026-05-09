using Project.Characters.Player.PlayerScripts.Core;
using UnityEngine;

namespace Project.Characters.Enemy.EnemyScripts.Combat
{
    public abstract class AttackContext
    {
        public Transform firePoint;
        public Transform player;
        public ObjectPool pool;
        public AttackData data;
        public Animator animator;
    }
}