﻿using System;
using System.Threading;
using BEPUphysics.DataStructures;
using System.Collections.ObjectModel;

namespace BEPUphysics.DeactivationManagement
{
    ///<summary>
    /// A collection of simulation island members bound together with connections.
    /// An island is activated and deactivated as a group.
    ///</summary>
    public class SimulationIsland
    {
        internal bool isActive = true;
        ///<summary>
        /// Gets whether or not the island is currently active.
        ///</summary>
        public bool IsActive
        {
            get
            {
                return isActive;
            }
        }
        internal RawList<ISimulationIslandMember> members = new RawList<ISimulationIslandMember>();
        internal int deactivationCandidateCount;

        ///<summary>
        /// Gets the list of members in the island.
        ///</summary>
        public ReadOnlyList<ISimulationIslandMember> Members
        {
            get
            {
                return new ReadOnlyList<ISimulationIslandMember>(members);
            }
        }

        //TODO: Readonly accessible members list?
        //TODO: Should members list be hash-based collections?
        //TODO: Should members list be sorted list?

        ///<summary>
        /// Constructs a simulation island.
        ///</summary>
        public SimulationIsland()
        {
            memberActivatedDelegate = MemberActivated;
            becameDeactivationCandidateDelegate = BecameDeactivationCandidate;
            becameNonDeactivationCandidateDelegate = BecameNonDeactivationCandidate;
        }

        Action<ISimulationIslandMember> memberActivatedDelegate;
        void MemberActivated(ISimulationIslandMember member)
        {
            Activate();
        }

        Action<ISimulationIslandMember> becameDeactivationCandidateDelegate;
        void BecameDeactivationCandidate(ISimulationIslandMember member)
        {
            Interlocked.Increment(ref deactivationCandidateCount);
            //The reason why this does not deactivate when count == members.count is that deactivation candidate count will go up and down in parallel.
            //The actual deactivation process is not designed to be thread safe.  Perhaps doable, but perhaps not worth the effort.
        }
        Action<ISimulationIslandMember> becameNonDeactivationCandidateDelegate;
        void BecameNonDeactivationCandidate(ISimulationIslandMember member)
        {
            Interlocked.Decrement(ref deactivationCandidateCount);
        }

        ///<summary>
        /// Activates the simulation island.
        ///</summary>
        public void Activate()
        {
            //TODO: CONSIDER ACTIVE ISLAND WITH FORCE-DEACTIVATED MEMBER.  ACTIVATING SIMULATION ISLAND WILL NOT WAKE FORCE-DEACTIVATED MEMBER.  DESIRED?
            if (!isActive)
            {
                isActive = true;
                for (int i = 0; i < members.count; i++)
                {
                    members.Elements[i].IsActive = true;
                }
            }
        }


        ///<summary>
        /// Attempts to deactivate the simulation island.
        ///</summary>
        ///<returns>Whether or not the simulation island was successfully deactivated.</returns>
        public bool TryToDeactivate()
        {
            //TODO: Check the deactivation count.  If it's a fully deactivated simulation island, then try to deactivate !:)
            //DO NOT WORRY ABOUT THREAD SAFETY HERE.
            //TryToDeactivate will be called sequentially in a 'limited work per frame' scheme.
            //Avoids load balancing problems and makes implementation easier.
            if (isActive && deactivationCandidateCount == members.Count)
            {
                isActive = false;
                for (int i = 0; i < members.count; i++)
                {
                    members.Elements[i].IsActive = false;
                }
                return true;
            }
            return false;

        }

        ///<summary>
        /// Adds a member to the simulation island.
        ///</summary>
        ///<param name="member">Member to add.</param>
        ///<exception cref="Exception">Thrown when the member being added is either non-dynamic or already has a simulation island.</exception>
        public void Add(ISimulationIslandMember member)
        {
            //This method is not thread safe.
            //TODO: Should it wake the island up?
            if (member.IsDynamic && member.SimulationIsland == null)
            {
                member.SimulationIsland = this;
                members.Add(member);
                member.Activated += memberActivatedDelegate;
                member.BecameDeactivationCandidate += becameDeactivationCandidateDelegate;
                member.BecameNonDeactivationCandidate += becameNonDeactivationCandidateDelegate;
                if (member.IsDeactivationCandidate)
                {
                    deactivationCandidateCount++;
                }
            }
            else
                throw new Exception("Member either is not dynamic or already has a simulation island; cannot add.");
        }

        ///<summary>
        /// Removes a member from the simulation island.
        ///</summary>
        ///<param name="member">Member to remove.</param>
        ///<exception cref="Exception">Thrown when the member does not belong to this simulation island.</exception>
        public void Remove(ISimulationIslandMember member)
        {
            //Is this method ever used?  What if old islands are simply cleared and a new one is repopulated instead?
            //More amenable to UFBRPC approach, probably quicker/simpler overall than removing even with lists
            //Consider a single block leaving a large island. BFS will quickly find out the necessary information to quickly
            //remove everything from the old island.
            //Event handlers will hold references still if not cleaned up via removal...

            //This method is not thread safe.
            //TODO: Should it wake the island up?
            if (member.SimulationIsland == this)
            {
                members.FastRemove(member);
                member.SimulationIsland = null;
                member.Activated -= memberActivatedDelegate;
                member.BecameDeactivationCandidate -= becameDeactivationCandidateDelegate;
                member.BecameNonDeactivationCandidate -= becameNonDeactivationCandidateDelegate;
                if (member.IsDeactivationCandidate)
                {
                    deactivationCandidateCount--;
                }
            }
            else
                throw new Exception("Member does not belong to island; cannot remove.");
        }


        internal void RemoveAt(int i)
        {
            //TODO: If this becomes a hash-based system, this method is pointless.
            ISimulationIslandMember member = members.Elements[i];
            members.FastRemoveAt(i);
            member.SimulationIsland = null;
            member.Activated -= memberActivatedDelegate;
            member.BecameDeactivationCandidate -= becameDeactivationCandidateDelegate;
            member.BecameNonDeactivationCandidate -= becameNonDeactivationCandidateDelegate;
            if (member.IsDeactivationCandidate)
            {
                deactivationCandidateCount--;
            }
        }

        internal void CleanUp()
        {
            isActive = true;
            deactivationCandidateCount = 0;
            members.Clear();
        }
    }
}
