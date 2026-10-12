export interface NavItem {
  id: string
  label: string
  href: string
}

export interface LearningPath {
  id: string
  name: string
  mark: string
  category: 'foundation' | 'advanced'
  level: string
  description: string
  topics: string[]
  tone: 'blue' | 'green' | 'yellow'
}
