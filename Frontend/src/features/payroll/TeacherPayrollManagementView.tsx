import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTeachers } from '@/features/teachers/api'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Input'
import { usePayrollRunsForTeacher } from './api'
import { GenerateRunModal } from './GenerateRunModal'
import { PayrollRunsList } from './PayrollRunsList'

export function TeacherPayrollManagementView() {
  const { data: teachers } = useTeachers()
  const [teacherId, setTeacherId] = useState('')
  const { data: runs, isLoading } = usePayrollRunsForTeacher(teacherId || null)
  const [isGenerateOpen, setIsGenerateOpen] = useState(false)

  return (
    <div>
      <div className="mb-4 flex items-center gap-3">
        <div className="w-64">
          <Select value={teacherId} onChange={(e) => setTeacherId(e.target.value)}>
            <option value="">Select a teacher...</option>
            {teachers?.map((teacher) => (
              <option key={teacher.id} value={teacher.id}>
                {teacher.fullName}
              </option>
            ))}
          </Select>
        </div>
        {teacherId && (
          <Button onClick={() => setIsGenerateOpen(true)}>
            <Plus size={15} />
            Generate run
          </Button>
        )}
      </div>

      {!teacherId && <p className="text-sm text-muted">Choose a teacher to see their payroll runs.</p>}
      {teacherId && isLoading && <p className="text-sm text-muted">Loading payroll runs...</p>}
      {teacherId && runs && <PayrollRunsList runs={runs} teacherId={teacherId} canManage />}

      {isGenerateOpen && teacherId && (
        <GenerateRunModal teacherId={teacherId} onClose={() => setIsGenerateOpen(false)} />
      )}
    </div>
  )
}
