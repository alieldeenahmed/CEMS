export interface Curriculum {
  id: string
  name: string
  description: string
}

export interface CurriculumInput {
  name: string
  description: string
}

export interface Subject {
  id: string
  name: string
  curriculumId: string
}

export interface SubjectInput {
  name: string
  curriculumId: string
}
