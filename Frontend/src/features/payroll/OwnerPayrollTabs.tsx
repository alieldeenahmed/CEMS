import { useState } from 'react'
import { NonTeachingStaffPayrollView } from './NonTeachingStaffPayrollView'
import { TeacherPayrollManagementView } from './TeacherPayrollManagementView'

export function OwnerPayrollTabs() {
  const [tab, setTab] = useState<'teachers' | 'staff'>('teachers')

  return (
    <div>
      <div className="mb-5 flex gap-1 border-b border-line">
        <button
          type="button"
          onClick={() => setTab('teachers')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'teachers' ? 'border-b-2 border-navy text-navy' : 'text-muted'
          }`}
        >
          Teachers
        </button>
        <button
          type="button"
          onClick={() => setTab('staff')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'staff' ? 'border-b-2 border-navy text-navy' : 'text-muted'
          }`}
        >
          Front Desk & Branch Managers
        </button>
      </div>

      {tab === 'teachers' ? <TeacherPayrollManagementView /> : <NonTeachingStaffPayrollView />}
    </div>
  )
}
