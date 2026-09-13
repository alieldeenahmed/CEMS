export interface Branch {
  id: string
  name: string
  address: string
  phone: string
  isActive: boolean
}

export interface BranchInput {
  name: string
  address: string
  phone: string
}

export interface Room {
  id: string
  branchId: string
  name: string
  capacity: number
}

export interface RoomInput {
  name: string
  capacity: number
}
