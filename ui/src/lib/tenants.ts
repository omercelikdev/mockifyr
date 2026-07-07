// Multi-tenancy is first-class in the engine: every admin call is scoped to a tenant and carries the
// X-Mockifyr-Tenant header. Until an admin `list tenants` endpoint exists these are seeded; the active
// tenant is persisted so a reload keeps the operator's context.
export interface Tenant {
  id: string
  name: string
}

export const TENANTS: Tenant[] = [
  { id: 'default', name: 'Default' },
  { id: 'acme-pay', name: 'Acme Payments' },
  { id: 'globex', name: 'Globex Retail' },
]

export const TENANT_HEADER = 'X-Mockifyr-Tenant'
