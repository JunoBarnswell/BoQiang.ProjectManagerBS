# Current Low-Code Contract Inventory

This inventory is the machine-readable baseline for HAO-13. The JSON file records the current source definitions, runtime support, migration status, and known gaps without treating the target Designer Document V3 contract as an implemented runtime.

Source of truth: [current-contract-inventory.json](./current-contract-inventory.json).

The inventory deliberately records the component list as incomplete until every concrete `RuntimeDesignerElement.type` registration is extracted from the renderer/component registry. HAO-13 must remain in review until that gap is closed and the generated inventory is checked against the source tree.
