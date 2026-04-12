# Paratrooper Architecture

## Overview
Paratrooper is the root entity (composition root) of the enemy system.

## Hierarchy

Paratrooper (Root / Brain)
- Model: data/state
- Controller: AI logic + state decisions
- StateMachine: state transitions
- View: Spine + VFX rendering
- DamageReceiver: hit handling
- BodyParts: per-limb collision system
- DeathHandler: death logic

## Rules
- No gameplay logic in root MonoBehaviour
- Systems communicate via events or interfaces
- View never modifies game state directly