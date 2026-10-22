import { contextExamCodes, goalFitsContext } from './student-goal-context';
describe('student goal context',()=>{
  it('offers only LGS for LGS and TYT plus the chosen YKS field',()=>{
    expect(contextExamCodes({purpose:'Lgs'} as any)).toEqual(['LGS']);
    expect(contextExamCodes({purpose:'Yks',primaryScoreType:'SAY'} as any)).toEqual(['TYT','AYT_SAY']);
    expect(contextExamCodes({purpose:'Yks',primaryScoreType:'YDT'} as any)).toEqual(['TYT','YDT_EN']);
  });
  it('keeps incompatible legacy goals out of the main target choices',()=>{
    expect(goalFitsContext({targetUniversityProgramId:'p'} as any,{purpose:'Lgs'} as any)).toBe(false);
    expect(goalFitsContext({targetSchoolId:'s'} as any,{purpose:'Yks'} as any)).toBe(false);
    expect(goalFitsContext({catalogTarget:{scoreType:'EA'}} as any,{purpose:'Yks',primaryScoreType:'SAY'} as any)).toBe(false);
  });
});
