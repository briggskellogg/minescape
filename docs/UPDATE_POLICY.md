# Update policy

World-critical gameplay and worldgen freeze at V1.0: Matcha 1.02 data, Terralith 2.6.4, Amplified Nether 1.2.15, Nullscape 1.2.20, generated compatibility/adapters, and first-party persistent-state schemas.

The only elective authored-content update is a later official Matcha release used as input to a resource-only derivative. The derivative begins with frozen 1.02 assets, accepts individually audited visual deltas, contains no `data/**`, preserves attribution/share-alike/noncommercial terms, passes full reference/controller/progression tests, and has instant hash rollback.

Operational maintenance—runtime, loader, performance, mapping, dashboard, backup, security—does not change world authorship. It is allowed only when necessary and must be tested on a restored MineJammer clone with a backup and rollback point. Never swap or remove a terrain generator in production.
